using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OsuRussianRep.Context;
using OsuRussianRep.Dtos;
using OsuSharp.Domain;
using OsuSharp.Interfaces;

namespace OsuRussianRep.Services;

public sealed class OsuUserCache : BackgroundService
{
    private readonly IOsuClient _osuClient;
    private readonly IMemoryCache _mem;
    private readonly IServiceProvider _sp;
    private readonly ILogger<OsuUserCache> _log;
    private readonly string _cacheDir;
    private readonly TimeSpan _ttl = TimeSpan.FromHours(6);
    private readonly SemaphoreSlim _rateLimit = new(1, 1);
    private readonly IMapper _mapper;

    public OsuUserCache(IOsuClient osuClient, IMemoryCache mem, IServiceProvider sp, ILogger<OsuUserCache> log, IMapper mapper)
    {
        _osuClient = osuClient;
        _mem = mem;
        _sp = sp;
        _log = log;
        _mapper = mapper;

        _cacheDir = Path.Combine(AppContext.BaseDirectory, "osu_cache");
        Directory.CreateDirectory(_cacheDir);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _log.LogInformation("OsuUserCache запускается...");

        // первичная прогрузка
        await LoadAllUsersAsync(ct);
        _log.LogInformation("Первичная загрузка osu-юзеров завершена.");

        // 🔁 цикл автообновления
        var interval = TimeSpan.FromHours(3);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                _log.LogInformation("Плановая проверка устаревших osu-юзеров...");
                await RefreshExpiredAsync(ct);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Ошибка при обновлении osu-кэша");
            }

            _log.LogInformation("Следующее обновление через {Hours}ч", interval.TotalHours);
            await Task.Delay(interval, ct);
        }
    }

    private async Task LoadAllUsersAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var users = await db.ChatUsers
            .AsNoTracking()
            .OrderByDescending(u => u.Messages.Count)
            .Take(50)
            .Select(u => u.Nickname)
            .ToListAsync(ct);

        foreach (var name in users)
        {
            try
            {
                // если уже есть в памяти — пропускаем
                if (_mem.TryGetValue<IUser>(name, out _))
                    continue;

                var file = Path.Combine(_cacheDir, $"{Sanitize(name)}.json");

                // если есть на диске и не протух — просто грузим
                if (File.Exists(file))
                {
                    try
                    {
                        var txt = await File.ReadAllTextAsync(file, ct);
                        var data = JsonSerializer.Deserialize<OsuUserFile>(txt);
                        if (data?.ExpiresAt > DateTime.UtcNow)
                        {
                            _mem.Set(name, data.OsuUser, _ttl);
                            continue; // ок, юзер свежий
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Ошибка чтения кэша {User}", name);
                    }
                }

                // иначе — качаем в фоне
                _ = Task.Run(() => EnsureUserCachedAsync(name, ct), ct);
                await Task.Delay(700, ct); // простенький rate-limit
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Ошибка при загрузке юзера {User}", name);
            }
        }
    }

    private async Task RefreshExpiredAsync(CancellationToken ct)
    {
        var files = Directory.GetFiles(_cacheDir, "*.json");
        var now = DateTime.UtcNow;
        var expired = new List<string>();

        foreach (var f in files)
        {
            try
            {
                var txt = await File.ReadAllTextAsync(f, ct);
                var data = JsonSerializer.Deserialize<OsuUserFile>(txt);
                if (data == null) continue;

                if (data.ExpiresAt <= now)
                    expired.Add(data.Username);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Ошибка проверки файла {File}", f);
            }
        }

        if (expired.Count == 0)
        {
            _log.LogInformation("Все osu-юзеры актуальны 👍");
            return;
        }

        _log.LogInformation("Обновляю {Count} устаревших osu-юзеров...", expired.Count);

        foreach (var name in expired)
        {
            _ = Task.Run(() => EnsureUserCachedAsync(name, ct), ct);
            await Task.Delay(700, ct); // простой rate-limit
        }
    }

    public async Task<CachedOsuUserDto?> GetUserAsync(string username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username))
            return null;

        // сначала память
        if (_mem.TryGetValue<IUser>(username, out var cached))
            return _mapper.Map<CachedOsuUserDto>(cached);

        // потом файл
        var file = Path.Combine(_cacheDir, $"{Sanitize(username)}.json");
        if (File.Exists(file))
        {
            try
            {
                var txt = await File.ReadAllTextAsync(file, ct);
                var data = JsonSerializer.Deserialize<OsuUserFile>(txt);
                if (data is not null && data.ExpiresAt > DateTime.UtcNow)
                {
                    _mem.Set(username, data.OsuUser, _ttl);
                    return _mapper.Map<CachedOsuUserDto>(data.OsuUser);
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Ошибка чтения кэша {User}", username);
            }
        }

        // если нет — грузим из API
        return await EnsureUserCachedAsync(username, ct);
    }

    private async Task<CachedOsuUserDto?> EnsureUserCachedAsync(string username, CancellationToken ct)
    {
        await _rateLimit.WaitAsync(ct);
        try
        {
            IUser? user = null;
            try
            {
                user = await _osuClient.GetUserAsync(username, GameMode.Osu, token: ct);
                if (user.GameMode != GameMode.Osu)
                    user = await _osuClient.GetUserAsync(username, user.GameMode, token: ct);
            }
            catch
            {
            }

            if (user == null)
                return null;

            _mem.Set(username, user, _ttl);

            var json = JsonSerializer.Serialize(new OsuUserFile
            {
                Username = username,
                CachedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow + _ttl,
                OsuUser = _mapper.Map<CachedOsuUserDto>(user)
            }, new JsonSerializerOptions {WriteIndented = true});

            var path = Path.Combine(_cacheDir, $"{Sanitize(username)}.json");
            await File.WriteAllTextAsync(path, json, ct);

            return _mapper.Map<CachedOsuUserDto>(user);
        }
        finally
        {
            _rateLimit.Release();
        }
    }

    private static string Sanitize(string username)
        => string.Join("_", username.Split(Path.GetInvalidFileNameChars()));

    private sealed class OsuUserFile
    {
        public string Username { get; set; }
        public DateTime CachedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
        public CachedOsuUserDto OsuUser { get; set; } = null!;
    }
}
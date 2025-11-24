using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OsuRussianRep.Context;
using OsuRussianRep.Helpers;
using OsuRussianRep.Interfaces;
using OsuRussianRep.Mapping;
using OsuRussianRep.Options;
using OsuRussianRep.Services;
using OsuSharp;
using OsuSharp.Extensions;
using Scalar.AspNetCore;

namespace OsuRussianRep;

internal class Program
{
    private const string CorsPolicyAny = "AllowAnyOrigin";

    public static async Task Main(string[] args)
    {
        Directory.CreateDirectory("data");
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        var builder = WebApplication.CreateBuilder(args);
        var cfg = builder.Configuration;

        builder.Configuration.AddEnvironmentVariables();

        var chatMode = cfg.GetValue<string>("ChatMode")?.Trim().ToLowerInvariant();

        ConfigureLogging(builder);
        ConfigureOptions(builder, cfg);
        ConfigureFrameworkServices(builder);
        ConfigureDb(builder, cfg);
        ConfigureDomainServices(builder);
        ConfigureChatMode(builder, chatMode);
        ConfigureOsuSharp(builder, cfg);
        ConfigureCors(builder);

        var app = builder.Build();

        ConfigureForwarding(app);
        ConfigureSwagger(app);
        ConfigureRouting(app);

        ApplyMigrations(app);
        InitIrcIfNeeded(app, cfg, chatMode);

        app.Run();
    }

    private static void ConfigureLogging(WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        builder.Logging.AddFilter("OsuSharp", LogLevel.None);
        builder.Logging.AddFilter("OsuSharp.Extensions", LogLevel.None);
        builder.Logging.AddFilter("OsuSharp.Net", LogLevel.None);
    }

    private static void ConfigureOptions(WebApplicationBuilder builder, IConfiguration cfg)
    {
        builder.Services.Configure<OsuApiOptions>(cfg.GetSection("Osu"));
        builder.Services.Configure<IrcConnectionOptions>(cfg.GetSection("IrcConnection"));
    }

    private static void ConfigureFrameworkServices(WebApplicationBuilder builder)
    {
        builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);

        builder.Services.AddOpenApi();
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
            c.SwaggerDoc("v1", new OpenApiInfo { Title = "OsuRussianRep API", Version = "v1" })
        );
    }

    private static void ConfigureDb(WebApplicationBuilder builder, IConfiguration cfg)
    {
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(cfg.GetConnectionString("DefaultConnection")));
    }

    private static void ConfigureDomainServices(WebApplicationBuilder builder)
    {
        builder.Services.AddMemoryCache();
        builder.Services.AddSingleton<OsuUserCache>();
        builder.Services.AddSingleton<IStopWordsProvider, StopWordsProvider>();
        builder.Services.AddSingleton<ChatCommandProcessor>();

        builder.Services.AddScoped<OsuService>();
        builder.Services.AddScoped<MessageService>();
        builder.Services.AddScoped<ReputationService>();
        builder.Services.AddScoped<IUsersService, UsersService>();
        builder.Services.AddScoped<IWordStatsService, WordStatsService>();
        builder.Services.AddScoped<IUserWordStatsService, UserWordStatsService>();

        builder.Services.AddHostedService<WordFrequencyIngestService>();
        builder.Services.AddHostedService<UserOsuSyncBackgroundService>();
    }

    private static void ConfigureChatMode(WebApplicationBuilder builder, string? chatMode)
    {
        switch (chatMode)
        {
            case "irc":
                Console.WriteLine("[Startup] Using IRC chat mode");
                builder.Services.AddSingleton<IrcMessageHandler>();
                builder.Services.AddSingleton<IIrcService, IrcService>();
                builder.Services.AddSingleton<IrcLogService>();
                break;

            case "webchat":
                Console.WriteLine("[Startup] Using WebChat mode");
                builder.Services.AddSingleton<OsuTokenStorage>();
                builder.Services.AddSingleton<OsuTokenService>();
                builder.Services.AddSingleton<OsuChannelStateStorage>();
                builder.Services.AddSingleton<WebMessageHandler>();
                builder.Services.AddSingleton<OsuWebChatService>();
                builder.Services.AddHostedService<OsuWebChatLoggerService>();
                break;

            default:
                throw new Exception("Invalid ChatMode. Use 'Irc' or 'WebChat' in appsettings.json");
        }
    }

    private static void ConfigureOsuSharp(WebApplicationBuilder builder, IConfiguration cfg)
    {
        builder.Host.ConfigureOsuSharp((_, options) =>
        {
            options.Configuration = new OsuClientConfiguration
            {
                ClientId = long.Parse(cfg.GetValue("OsuApi:ClientId", "0")),
                ClientSecret = cfg.GetValue("OsuApi:ClientSecret", "")
            };
        });
    }

    private static void ConfigureCors(WebApplicationBuilder builder)
    {
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyAny, policy =>
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
        });
    }

    private static void ConfigureForwarding(WebApplication app)
    {
        app.UseForwardedHeaders(new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                               ForwardedHeaders.XForwardedProto |
                               ForwardedHeaders.XForwardedHost,
            ForwardLimit = 1,
            KnownNetworks = { },
            KnownProxies = { }
        });

        app.Use((ctx, next) =>
        {
            var prefix = ctx.Request.Headers["X-Forwarded-Prefix"].FirstOrDefault();
            if (!string.IsNullOrEmpty(prefix))
                ctx.Request.PathBase = prefix;

            return next();
        });
    }

    private static void ConfigureSwagger(WebApplication app)
    {
        app.UseSwagger(c =>
        {
            c.PreSerializeFilters.Add(ApplyForwardedServerUrl);
            c.RouteTemplate = "api/swagger/{documentName}/swagger.json";
        });

        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/api/swagger/v1/swagger.json", "OsuRussianRep API");
            c.RoutePrefix = "api/swagger";
        });

        app.MapGroup("/api").MapScalarApiReference(options =>
        {
            options
                .WithTitle("OsuRussianRep API")
                .WithTheme(ScalarTheme.DeepSpace)
                .WithOpenApiRoutePattern("/api/swagger/v1/swagger.json");
        });
    }

    private static void ConfigureRouting(WebApplication app)
    {
        app.UseCors(CorsPolicyAny);
        app.UseAuthorization();
        app.MapControllers();
    }

    private static void ApplyMigrations(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }

    private static void InitIrcIfNeeded(WebApplication app, IConfiguration cfg, string? chatMode)
    {
        if (chatMode != "irc")
            return;

        var irc = app.Services.GetRequiredService<IIrcService>();
        var handler = app.Services.GetRequiredService<IrcMessageHandler>();
        var lifetime = app.Lifetime;

        irc.ChannelMessageReceived += async (_, e)
            => await handler.HandleChannelMessageAsync(e.Channel, e.Nick, e.Text);
        irc.PrivateMessageReceived += async (_, e)
            => await handler.HandlePrivateMessageAsync(e.Nick, e.Text);
        irc.WhoisMessageReceived += async (_, e)
            => await handler.HandleWhoisAsync(e.Nick, e.ProfileUrl);

        lifetime.ApplicationStarted.Register(async void () =>
        {
            try
            {
                await irc.ConnectAsync();
                await irc.JoinAsync(cfg["IrcConnection:Channel"]);
            }
            catch (Exception ex)
            {
                app.Logger.LogError(ex, "IRC: failed to connect on start");
            }
        });

        lifetime.ApplicationStopping.Register(async void () =>
        {
            try { await irc.DisconnectAsync("shutdown"); }
            catch
            {
                // ignored
            }
        });
    }

    private static void ApplyForwardedServerUrl(OpenApiDocument swagger, HttpRequest req)
    {
        var scheme = (string?)req.Headers["X-Forwarded-Proto"] ?? req.Scheme;
        var host = (string?)req.Headers["X-Forwarded-Host"] ?? req.Host.Value;

        swagger.Servers = new[] { new OpenApiServer { Url = $"{scheme}://{host}" } }.ToList();
    }
}

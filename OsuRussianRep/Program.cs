using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OsuRussianRep.Context;
using OsuRussianRep.Helpers;
using OsuRussianRep.Interfaces;
using OsuRussianRep.Mapping;
using OsuRussianRep.Services;
using OsuSharp;
using OsuSharp.Extensions;
using Scalar.AspNetCore;

namespace OsuRussianRep;

internal class Program
{
    private const string CorsPolicyAny = "AllowAnyOrigin";

    private static async Task Main(string[] args)
    {
        Directory.CreateDirectory("data");
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        var builder = WebApplication.CreateBuilder(args);

        builder.Configuration.AddEnvironmentVariables();
        var cfg = builder.Configuration;

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Logging.AddFilter("OsuSharp", LogLevel.None);
        builder.Logging.AddFilter("OsuSharp.Extensions", LogLevel.None);
        builder.Logging.AddFilter("OsuSharp.Net", LogLevel.None);

        builder.Services.AddAutoMapper(typeof(MappingProfile).Assembly);
        builder.Services.AddOpenApi();
        builder.Services.AddControllers();

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo {Title = "OsuRussianRep API", Version = "v1"});
        });

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(cfg.GetConnectionString("DefaultConnection")));

        builder.Services.AddSingleton<IrcMessageHandler>();
        builder.Services.AddSingleton<IStopWordsProvider, StopWordsProvider>();
        builder.Services.AddSingleton<IIrcService, IrcService>();
        builder.Services.AddSingleton<IrcLogService>();

        builder.Services.AddScoped<OsuService>();
        builder.Services.AddScoped<MessageService>();
        builder.Services.AddScoped<ReputationService>();
        builder.Services.AddScoped<IUsersService, UsersService>();
        builder.Services.AddScoped<IWordStatsService, WordStatsService>();
        builder.Services.AddScoped<IUserWordStatsService, UserWordStatsService>();

            //builder.Services.AddHostedService<WordFrequencyIngestService>();
        builder.Services.AddHostedService<UserOsuSyncBackgroundService>();
        builder.Services.AddHostedService<IrcLogService>();

        builder.Services.AddSingleton<OsuUserCache>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<OsuUserCache>());

        builder.Services.AddMemoryCache();

        builder.Host.ConfigureOsuSharp((_, options) =>
        {
            options.Configuration = new OsuClientConfiguration
            {
                ClientId = long.Parse(cfg.GetValue("OsuApi:ClientId", "0")),
                ClientSecret = cfg.GetValue("OsuApi:ClientSecret", "")
            };
        });

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(CorsPolicyAny, policy =>
                policy.AllowAnyOrigin()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
        });

        var app = builder.Build();

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
            // если nginx передал префикс — уважаем его
            var prefix = ctx.Request.Headers["X-Forwarded-Prefix"].FirstOrDefault();
            if (!string.IsNullOrEmpty(prefix))
                ctx.Request.PathBase = prefix;
            return next();
        });


        // Swagger (с учётом X-Forwarded-* для корректных серверов)
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

        app.UseCors(CorsPolicyAny);
        app.UseAuthorization();
        app.MapControllers();

        // Миграции
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        }

        var irc = app.Services.GetRequiredService<IIrcService>();
        var handler = app.Services.GetRequiredService<IrcMessageHandler>();
        var lifetime = app.Lifetime;

        irc.ChannelMessageReceived += async (_, e)
            => await handler.HandleChannelMessageAsync(e.Channel, e.Nick, e.Text);
        irc.PrivateMessageReceived += async (_, e)
            =>
        {
            await handler.HandlePrivateMessageAsync(e.Nick, e.Text);
        };
        irc.WhoisMessageReceived += async (_, e)
            =>
        {
            await handler.HandleWhoisAsync(e.Nick, e.ProfileUrl);
        };

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
            try
            {
                await irc.DisconnectAsync("shutdown");
            }
            catch
            {
                /* ignore */
            }
        });

        app.Run();

        static void ApplyForwardedServerUrl(OpenApiDocument swagger, HttpRequest req)
        {
            var scheme = (string?) req.Headers["X-Forwarded-Proto"] ?? req.Scheme;
            var host = (string?) req.Headers["X-Forwarded-Host"] ?? req.Host.Value;
            swagger.Servers = new[] {new OpenApiServer {Url = $"{scheme}://{host}"}}.ToList();
        }
    }
}
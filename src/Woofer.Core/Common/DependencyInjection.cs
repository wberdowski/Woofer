using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using System.Reflection;
using Woofer.Core.Config;
using Woofer.Core.Interfaces;
using Woofer.Core.Modules.AudioPlayerModule;
using YoutubeExplode;

namespace Woofer.Core.Common
{
    internal static class DependencyInjection
    {
        public static IServiceCollection AddBotServices(this IServiceCollection services)
        {
            var logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .WriteTo.File("logs/log.txt", rollingInterval: RollingInterval.Day)
            .WriteTo.Console()
            .CreateLogger();

            return services
                .AddLogging(c =>
                {
                    c.AddSerilog(logger);
                })
                .AddConfig()
                .AddDiscord()
                .AddSearchServices()
                .AddAudio()
                .AddBotModules();
        }

        public static IServiceCollection AddAudio(this IServiceCollection services)
        {
            return services
                .AddSingleton<AudioPlayerManager>()
                .AddScoped<AudioPlayer>();
        }

        public static IServiceCollection AddBotModules(this IServiceCollection services)
        {
            var moduleTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t =>
                    t.BaseType != null &&
                    t.BaseType.IsGenericType &&
                    t.BaseType.GetGenericTypeDefinition() == typeof(AppModule<>)
                );

            foreach (var moduleType in moduleTypes)
            {
                services = services.AddSingleton(typeof(IAppModule), moduleType);
            }

            return services.AddSingleton<AppModuleManager>();
        }

        public static IServiceCollection AddSearchServices(this IServiceCollection services)
        {
            return services
                .AddSingleton<YoutubeClient>()
                .AddSingleton<SearchProvider>();
        }

        private static IServiceCollection AddDiscord(this IServiceCollection services)
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged & ~(GatewayIntents.GuildInvites | GatewayIntents.GuildScheduledEvents)
            };

            return services.AddSingleton(config)
                .AddSingleton<DiscordSocketClient>();
        }

        private static IServiceCollection AddConfig(this IServiceCollection services)
        {
            return services.AddSingleton<ConfigManager>();
        }
    }
}

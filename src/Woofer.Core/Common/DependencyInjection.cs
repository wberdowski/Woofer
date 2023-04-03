using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Woofer.Core.Config;
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
                .AddAudio();
        }

        public static IServiceCollection AddAudio(this IServiceCollection services)
        {
            return services
                .AddSingleton<AudioPlayerManager>()
                .AddScoped<AudioPlayer>();
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
                .AddSingleton<DiscordSocketClient>()
                .AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()))
                .AddSingleton<InteractionHandler>();
        }

        private static IServiceCollection AddConfig(this IServiceCollection services)
        {
            return services.AddSingleton<ConfigManager>();
        }
    }
}

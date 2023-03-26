using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Woofer.Core.Audio;
using Woofer.Core.Common.Interfaces;
using Woofer.Core.Config;
using Woofer.Core.Modules;
using YoutubeExplode;

namespace Woofer.Core.Common
{
    internal static class DependencyInjection
    {
        public static IServiceCollection AddBotServices(this IServiceCollection services)
        {
            return services
                .AddLogging(c =>
                {
#if DEBUG
                    c.SetMinimumLevel(LogLevel.Debug);
#else
                    c.SetMinimumLevel(LogLevel.Information);
#endif
                    c.AddSimpleConsole(o =>
                    {
                        o.UseUtcTimestamp = true;
                        o.TimestampFormat = "HH:mm:ss ";
                        o.SingleLine = false;
                    });
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
            return services
                .AddSingleton<IAppModule, HelpModule>()
                .AddSingleton<IAppModule, AudioPlayerModule>();
        }

        public static IServiceCollection AddSearchServices(this IServiceCollection services)
        {
            return services
                .AddSingleton<YoutubeClient>();
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

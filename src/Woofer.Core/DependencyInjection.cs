using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Woofer.Core.Audio;
using Woofer.Core.Config;
using Woofer.Core.Modules;
using YoutubeExplode;

namespace Woofer.Core
{
    internal static class DependencyInjection
    {
        public static IServiceCollection AddBotServices(this IServiceCollection collection)
        {
            return collection
                .AddConfig()
                .AddLogger()
                .AddDiscord()
                .AddSearchServices()
                .AddAudio();
        }

        public static IServiceCollection AddAudio(this IServiceCollection collection)
        {
            return collection
                .AddSingleton<AudioPlayerManager>();
        } 
        
        public static IServiceCollection AddBotModules(this IServiceCollection collection)
        {
            return collection
                .AddSingleton<IAppModule, HelpModule>()
                .AddSingleton<IAppModule, AudioPlayerModule>();
        } 
        
        public static IServiceCollection AddSearchServices(this IServiceCollection collection)
        {
            return collection
                .AddSingleton<YoutubeClient>();
        }

        private static IServiceCollection AddDiscord(this IServiceCollection collection)
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged & ~(GatewayIntents.GuildInvites | GatewayIntents.GuildScheduledEvents)
            };

            return collection.AddSingleton(config)
                .AddSingleton<DiscordSocketClient>();
        }

        private static IServiceCollection AddLogger(this IServiceCollection collection)
        {
            var logger = new LoggerConfiguration()
#if DEBUG || WSL
               .MinimumLevel.Debug()
#endif
               .WriteTo.Console()
               .CreateLogger();

            return collection.AddSingleton<ILogger>(logger);
        }

        private static IServiceCollection AddConfig(this IServiceCollection collection)
        {
            return collection.AddSingleton<ConfigManager>();
        }
    }
}

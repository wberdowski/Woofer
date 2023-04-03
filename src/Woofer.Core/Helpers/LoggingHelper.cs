using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;

namespace Woofer.Core.Helpers
{
    internal static class LoggingHelper
    {
        public static void RegisterLogger(this DiscordSocketClient client, ILogger logger)
        {
            client.Log += async (msg) =>
            {
                var severity = msg.Severity switch
                {
                    LogSeverity.Critical => LogLevel.Critical,
                    LogSeverity.Error => LogLevel.Error,
                    LogSeverity.Warning => LogLevel.Warning,
                    LogSeverity.Info => LogLevel.Information,
                    LogSeverity.Verbose => LogLevel.Trace,
                    LogSeverity.Debug => LogLevel.Debug,
                    _ => LogLevel.Information
                };

                logger.Log(severity, msg.Exception, "[{Source}] {Message}", msg.Source, msg.Message);

                await Task.CompletedTask;
            };
        }
    }
}
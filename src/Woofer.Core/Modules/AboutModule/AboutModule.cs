using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Woofer.Core.Common;
using Woofer.Core.Helpers;

namespace Woofer.Core.Modules.HelpModule
{
    internal class AboutModule : AppModule<AboutModule>
    {
        private readonly AppModuleManager _appModuleManager;

        public AboutModule(ILogger<AboutModule>? logger, AppModuleManager appModuleManager) : base(logger)
        {
            _appModuleManager = appModuleManager;
        }

        public override IEnumerable<ApplicationCommandProperties> RegisterCommands()
        {
            RegisterCommand("about", "Show about.", HandleAboutCommand);
            RegisterCommand("help", "Show help.", HandleHelpCommand);

            return base.RegisterCommands();
        }

        private async Task HandleAboutCommand(SocketSlashCommand command)
        {
            var proc = Process.GetCurrentProcess();
            var uptime = (DateTime.UtcNow - proc.StartTime.ToUniversalTime());

            var embed = new EmbedBuilder()
                .WithColor(Color.DarkPurple)
                .WithAuthor("About Woofer")
                .WithThumbnailUrl("https://imgur.com/t4mYCC4.png")
                .WithDescription(
                    $"Version:\t**{AssemblyHelper.GetVersion()}**\n" +
                    $"Uptime:\t**{Math.Floor(uptime.TotalDays)}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s**\n" +
                    $"Process:\t**{proc.ProcessName} ({proc.Id})**\n" +
                    $"Memory usage:\t**{proc.PrivateMemorySize64 / 1024 / 1024} MB**\n" +
                    $"\n" +
                    $"> Type ``/help`` to view all available commands."
                );

            await command.RespondAsync(embed: embed.Build(), ephemeral: true);
        }

        private async Task HandleHelpCommand(SocketSlashCommand command)
        {
            await command.RespondAsync(
                "**Commands**\n" +
                "- /play\n" +
                "- /stop",
                ephemeral: true
            );
        }
    }
}

using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Globalization;
using Woofer.Core.Common;
using Woofer.Core.Helpers;

namespace Woofer.Core.Modules.HelpModule
{
    internal class AboutModule : AppModule<AboutModule>
    {
        private readonly AppModuleManager _appModuleManager;
        private readonly DiscordSocketClient _discordSocketClient;

        public AboutModule(ILogger<AboutModule>? logger, AppModuleManager appModuleManager, DiscordSocketClient discordSocketClient) : base(logger)
        {
            _appModuleManager = appModuleManager;
            _discordSocketClient = discordSocketClient;
        }

        public override IEnumerable<SlashCommandProperties> RegisterCommands()
        {
            RegisterCommand("about", "Show about.", HandleAboutCommand);
            RegisterCommand("help", "Show help.", HandleHelpCommand);
            RegisterCommand("debug", "Show debugging information.", HandleDebugCommand);

            return base.RegisterCommands();
        }

        private async Task HandleAboutCommand(SocketSlashCommand command)
        {
            var embed = new EmbedBuilder()
                .WithColor(Color.DarkPurple)
                .WithAuthor("About Woofer")
                .WithThumbnailUrl("https://imgur.com/t4mYCC4.png")
                .WithDescription(
                    $"Version:\t**{AssemblyHelper.GetVersion()}**\n" +
                    $"Guilds:\t**{_discordSocketClient.Guilds.Count}**\n" +
                    $"\n" +
                    $"> Type ``/help`` to view all available commands."
                );

            await command.RespondAsync(embed: embed.Build(), ephemeral: true);
        }

        private async Task HandleHelpCommand(SocketSlashCommand command)
        {
            var commands = _appModuleManager.RegisteredCommands;

            var embed = new EmbedBuilder()
            .WithColor(Color.DarkPurple)
            .WithAuthor("📋 Commands")
            .WithDescription(
                string.Join("\n", commands!.Select(c => $"**/{c.Name}** - {c.Description}"))
            );

            await command.RespondAsync(embed: embed.Build(), ephemeral: true);
        }

        [RequireOwner]
        private async Task HandleDebugCommand(SocketSlashCommand command)
        {
            var proc = Process.GetCurrentProcess();
            var uptime = (DateTime.UtcNow - proc.StartTime.ToUniversalTime());

            var embed = new EmbedBuilder()
            .WithColor(Color.DarkPurple)
            .WithAuthor("Debug information")
            .WithDescription(
                $"Uptime:\t**{Math.Floor(uptime.TotalDays)}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s**\n" +
                $"Process:\t**{proc.ProcessName} (PID {proc.Id})**\n" +
                $"Memory usage:\t**{proc.PrivateMemorySize64 / 1024 / 1024} MB**\n"
            );

            await command.RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}

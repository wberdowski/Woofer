using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Diagnostics;
using Woofer.Core.Helpers;

namespace Woofer.Core.Modules.HelpModule
{
    public class AboutModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly DiscordSocketClient _discordSocketClient;
        private readonly InteractionService _interactionService;

        public AboutModule(DiscordSocketClient discordSocketClient, InteractionService interactionService)
        {
            _discordSocketClient = discordSocketClient;
            _interactionService = interactionService;
        }

        [SlashCommand("about", "Show about section")]
        public async Task HandleAboutCommand()
        {
            var command = (ISlashCommandInteraction)Context.Interaction;

            var embed = new EmbedBuilder()
                .WithColor(Color.DarkPurple)
                .WithAuthor("About Woofer")
                .WithThumbnailUrl("https://imgur.com/t4mYCC4.png")
                .WithDescription(
                    $"Woofer is an open-source, self-hosted Discord music bot built using Discord.Net and running on .NET. " +
                    $"For more details please visit https://github.com/wberdowski/Woofer\n" +
                    $"\n" +
                    $"Version:\t**{AssemblyHelper.GetVersion()}**\n" +
                    $"# of guilds served:\t**{_discordSocketClient.Guilds.Count}**\n" +
                    $"\n" +
                    $"> Type ``/help`` to view all available commands."
                );

            await command.RespondAsync(embed: embed.Build(), ephemeral: true);
        }

        [SlashCommand("help", "Show help")]
        public async Task HandleHelpCommand()
        {
            var command = (ISlashCommandInteraction)Context.Interaction;

            var embed = new EmbedBuilder()
            .WithColor(Color.DarkPurple)
            .WithAuthor("📋 Commands")
            .WithDescription(
                string.Join("\n", _interactionService.SlashCommands.Select(c => $"**/{c.Name}** - {c.Description}"))
            );

            await command.RespondAsync(embed: embed.Build(), ephemeral: true);
        }

        [RequireOwner]
        [SlashCommand("debug", "Show debugging information")]
        public async Task HandleDebugCommand()
        {
            var command = (ISlashCommandInteraction)Context.Interaction;
            var proc = Process.GetCurrentProcess();
            var uptime = (DateTime.UtcNow - proc.StartTime.ToUniversalTime());

            var embed = new EmbedBuilder()
            .WithColor(Color.DarkPurple)
            .WithAuthor("Debug information")
            .WithDescription(
                $"Version:\t**{AssemblyHelper.GetVersion()}**\n" +
                $"Uptime:\t**{Math.Floor(uptime.TotalDays)}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s**\n" +
                $"Process:\t**{proc.ProcessName} (PID {proc.Id})**\n" +
                $"Memory usage:\t**{proc.PrivateMemorySize64 / 1024 / 1024} MB**\n" +
                $"Runtime version:\t**{Environment.Version}**"
            );

            await command.RespondAsync(embed: embed.Build(), ephemeral: true);
        }
    }
}

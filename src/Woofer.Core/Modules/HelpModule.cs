using Discord;
using Discord.WebSocket;
using Woofer.Core.Common;

namespace Woofer.Core.Modules
{
    internal class HelpModule : AppModule
    {
        public override IEnumerable<ApplicationCommandProperties> RegisterCommands()
        {
            var properties = new List<ApplicationCommandProperties>();

            var cmd = new SlashCommandBuilder()
                .WithName("help")
                .WithDescription("Show help.");

            properties.Add(cmd.Build());

            return properties;
        }

        public override async Task HandleCommand(SocketSlashCommand command)
        {
            if (command.CommandName == "help")
            {
                await command.RespondAsync($"Help should be here", ephemeral: true);
            }
        }
    }
}

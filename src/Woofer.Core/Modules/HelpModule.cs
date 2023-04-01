using Discord;
using Discord.WebSocket;
using Woofer.Core.Common;

namespace Woofer.Core.Modules
{
    internal class HelpModule : AppModule
    {
        public override IEnumerable<ApplicationCommandProperties> RegisterCommands()
        {
            RegisterCommand("help", "Show help.", HandleHelpCommand);

            return base.RegisterCommands();
        }

        private async Task HandleHelpCommand(SocketSlashCommand command)
        {
            await command.RespondAsync("**Commands**\n" +
                "- /play\n" +
                "- /stop", 
                ephemeral: true
            );
        }
    }
}

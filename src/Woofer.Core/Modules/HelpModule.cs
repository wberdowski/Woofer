using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Woofer.Core.Common;

namespace Woofer.Core.Modules
{
    internal class HelpModule : AppModule<HelpModule>
    {
        public HelpModule(ILogger<HelpModule>? logger) : base(logger)
        {
        }

        public override IEnumerable<ApplicationCommandProperties> GetRegisteredCommands()
        {
            RegisterCommand("help", "Show help.", HandleHelpCommand);

            return base.GetRegisteredCommands();
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

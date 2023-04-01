using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using Woofer.Core.Common;

namespace Woofer.Core.Modules.HelpModule
{
    internal class HelpModule : AppModule<HelpModule>
    {
        private readonly AppModuleManager _appModuleManager;

        public HelpModule(ILogger<HelpModule>? logger, AppModuleManager appModuleManager) : base(logger)
        {
            _appModuleManager = appModuleManager;
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

using Discord;
using Discord.WebSocket;
using Woofer.Core.Common.Interfaces;

namespace Woofer.Core.Common
{
    internal abstract class AppModule : IAppModule
    {
        public abstract Task HandleCommand(SocketSlashCommand command);
        public abstract IEnumerable<ApplicationCommandProperties> RegisterCommands();
        public virtual async Task HandleButtonExecuted(SocketMessageComponent component)
        {
            await Task.CompletedTask;
        }

    }
}

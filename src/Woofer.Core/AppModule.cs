using Discord;
using Discord.WebSocket;

namespace Woofer.Core
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

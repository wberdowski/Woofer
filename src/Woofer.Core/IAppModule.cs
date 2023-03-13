using Discord;
using Discord.WebSocket;

namespace Woofer.Core
{
    internal interface IAppModule
    {
        IEnumerable<ApplicationCommandProperties> RegisterCommands();
        Task HandleCommand(SocketSlashCommand command);
        Task HandleButtonExecuted(SocketMessageComponent component);
    }
}

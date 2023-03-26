using Discord;
using Discord.WebSocket;

namespace Woofer.Core.Common.Interfaces
{
    internal interface IAppModule
    {
        IEnumerable<ApplicationCommandProperties> RegisterCommands();
        Task HandleCommand(SocketSlashCommand command);
        Task HandleButtonExecuted(SocketMessageComponent component);
    }
}

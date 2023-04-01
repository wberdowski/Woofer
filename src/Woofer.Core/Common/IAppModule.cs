using Discord;
using Discord.WebSocket;

namespace Woofer.Core.Common
{
    internal interface IAppModule
    {
        Task HandleButtonExecuted(SocketMessageComponent component);
        void InvokeButtonExecuted(SocketMessageComponent component);
        void InvokeHandleCommand(SocketSlashCommand command);
        IEnumerable<ApplicationCommandProperties> GetRegisteredCommands();
    }
}
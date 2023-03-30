using Discord;
using Discord.WebSocket;

namespace Woofer.Core.Common
{
    internal class SlashCommandDefinition
    {
        public delegate Task SlashCommandHandler(SocketSlashCommand command);
        public SlashCommandProperties CommandProperties { get; set; }
        public SlashCommandHandler Method { get; set; }

        public SlashCommandDefinition(SlashCommandProperties commandProperties, SlashCommandHandler method)
        {
            CommandProperties = commandProperties;
            Method = method;
        }
    }
}

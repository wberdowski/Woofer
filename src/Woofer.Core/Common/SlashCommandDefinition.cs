using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using System.Reflection;

namespace Woofer.Core.Common
{
    internal class SlashCommandDefinition
    {
        public delegate Task SlashCommandHandler(SocketSlashCommand command);
        public SlashCommandProperties CommandProperties { get; }
        public SlashCommandHandler Method { get; }
        public bool RequrieGuild { get; }

        public SlashCommandDefinition(SlashCommandProperties commandProperties, SlashCommandHandler method)
        {
            CommandProperties = commandProperties;
            Method = method;

            RequrieGuild = method.Method.GetCustomAttribute<RequireContextAttribute>()?.Contexts.HasFlag(ContextType.Guild) ?? false;
        }
    }
}

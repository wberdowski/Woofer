using Discord;
using Woofer.Core.Audio;

namespace Woofer.Core.Common
{
    internal class PlayMessageInfo
    {
        public IDiscordInteraction Interaction { get; set; }
        public Embed Embed { get; set; }
        public Track Track { get; set; }

        public PlayMessageInfo(IDiscordInteraction interaction, Embed embed, Track track)
        {
            Interaction = interaction;
            Embed = embed;
            Track = track;
        }

        public async Task<bool> TryRevokeControls()
        {
            try
            {
                await Interaction.ModifyOriginalResponseAsync(m =>
                {
                    m.Embed = Embed;
                    m.Components = null;
                });

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
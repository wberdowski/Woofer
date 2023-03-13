using Discord;
using Woofer.Core.Common;

namespace Woofer.Core.Audio
{
    public class TrackMessageEventArgs : EventArgs
    {
        public ITrack Track { get; }
        public IUserMessage? Reply { get; }

        public TrackMessageEventArgs(ITrack track, IUserMessage? reply)
        {
            Track = track;
            Reply = reply;
        }
    }
}
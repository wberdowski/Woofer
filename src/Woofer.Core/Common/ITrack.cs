using Woofer.Core.Audio;

namespace Woofer.Core.Common
{
    public interface ITrack
    {
        public string Title { get; }
        public TimeSpan? Duration { get; }
        public TimeSpan? ResumeTime { get; }
        public string Url { get; }
        public string? ThumbnailUrl { get; }
        public WebAudioSource AudioSource { get; }
    }
}

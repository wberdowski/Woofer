using Woofer.Core.Audio;

namespace Woofer.Core.Common
{
    internal class WebSearchResult : ITrack
    {
        public string Title { get; set; }
        public TimeSpan? Duration { get; set; }
        public TimeSpan? ResumeTime { get; set; }
        public string Url { get; set; }
        public string? ThumbnailUrl { get; set; }
        public WebAudioSource AudioSource { get; set; }

        public WebSearchResult(string title, TimeSpan? duration, TimeSpan? resumeTime, string url, string? thumbnailUrl, WebAudioSource audioSource)
        {
            Title = title;
            Duration = duration;
            ResumeTime = resumeTime;
            Url = url;
            ThumbnailUrl = thumbnailUrl;
            AudioSource = audioSource;
        }

        public override string ToString()
        {
            return $"{Title}, {Duration}";
        }
    }
}

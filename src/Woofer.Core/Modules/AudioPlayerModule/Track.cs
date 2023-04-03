namespace Woofer.Core.Modules.AudioPlayerModule
{
    public abstract class Track
    {
        public string Id => new string(((uint)GetHashCode()).ToString().Select(c => (char)(c - '0' + 'a')).ToArray()).ToUpper();
        public string Title { get; }
        public TimeSpan? Duration { get; }
        public TimeSpan? ResumeTime { get; }
        public string Url { get; }
        public string? ThumbnailUrl { get; }
        public WebAudioSource AudioSource { get; }

        protected Track()
        {
        }

        protected Track(string title, TimeSpan? duration, TimeSpan? resumeTime, string url, string? thumbnailUrl, WebAudioSource audioSource)
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
            return $"{Id}, {Title}, {Duration}";
        }
    }
}

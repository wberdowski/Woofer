namespace Woofer.Core.Modules.AudioPlayerModule
{
    internal class WebSearchResult : Track
    {
        public WebSearchResult(
            string title,
            TimeSpan? duration,
            TimeSpan? resumeTime,
            string url,
            string? thumbnailUrl,
            WebAudioSource audioSource) : base(title, duration, resumeTime, url, thumbnailUrl, audioSource)
        {
        }
    }
}

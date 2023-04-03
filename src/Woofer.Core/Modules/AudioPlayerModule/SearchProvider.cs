using YoutubeExplode;

namespace Woofer.Core.Modules.AudioPlayerModule
{
    public class SearchProvider
    {
        private readonly YoutubeClient _ytClient;

        public SearchProvider(YoutubeClient ytClient)
        {
            _ytClient = ytClient;
        }

        public async Task<WebSearchResult> Search(string? searchQuery)
        {
            var results = _ytClient.Search.GetResultsAsync(searchQuery);
            var result = results.FirstAsync().Result;
            var video = await _ytClient.Videos.GetAsync(result.Url);
            var manifest = await _ytClient.Videos.Streams.GetManifestAsync(result.Url);

            // Select best quality
            var audioSource = manifest.GetAudioOnlyStreams()
                .Where(x => x.AudioCodec == "opus")
                .OrderByDescending(x => x.Bitrate.KiloBitsPerSecond)
                .First();

            var track = new WebSearchResult(
                video.Title,
                video.Duration,
                TimeSpan.Zero,
                video.Url,
                video.Thumbnails.FirstOrDefault()?.Url,
                new WebAudioSource(
                    (int)Math.Ceiling(audioSource.Bitrate.KiloBitsPerSecond),
                    audioSource.AudioCodec,
                    audioSource.Url
                )
            );
            return track;
        }
    }
}

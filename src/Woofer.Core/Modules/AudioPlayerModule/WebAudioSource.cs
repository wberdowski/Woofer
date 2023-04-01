namespace Woofer.Core.Modules.AudioPlayerModule
{
    public struct WebAudioSource
    {
        /// <summary>
        /// Bitrate in kilobits per second
        /// </summary>
        public int Bitrate { get; }
        public string Codec { get; }
        public string Url { get; }

        public WebAudioSource(int bitrate, string codec, string url)
        {
            Bitrate = bitrate;
            Codec = codec;
            Url = url;
        }
    }
}

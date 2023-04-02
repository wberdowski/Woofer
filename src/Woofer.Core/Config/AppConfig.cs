namespace Woofer.Core.Config
{
    [Serializable]
    internal class AppConfig
    {
        public string BotToken { get; set; } = string.Empty;
        public ulong? OwnerId { get; set; }
    }
}

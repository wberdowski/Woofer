using System.Text.Json;

namespace Woofer.Core.Config
{
    internal class ConfigManager
    {
        public const string ConfigFilePath = "config.json";
        public AppConfig? Config { get; set; }

        public async Task Load()
        {
            if (!File.Exists(ConfigFilePath))
            {
                Config = new AppConfig();
                await Save();
                return;
            }

            var data = await File.ReadAllTextAsync(ConfigFilePath);
            Config = JsonSerializer.Deserialize<AppConfig>(data);
        }

        public async Task Save()
        {
            var data = JsonSerializer.Serialize(Config, new JsonSerializerOptions() { WriteIndented = true });
            await File.WriteAllTextAsync(ConfigFilePath, data);
        }
    }
}

using Newtonsoft.Json;
using System.IO;

namespace OBSMusicPlayer;

public class AppConfig
{
    public string ObsUrl { get; set; } = "ws://localhost:4455";
    public string ObsPassword { get; set; } = "";
    public string MusicFolder { get; set; } = "";
    public string SourceText { get; set; } = "CurrentSong";
    public double HistoryRatio { get; set; } = 0.5;
    public int MaxRetries { get; set; } = 3;
    public float Volume { get; set; } = 0.7f;

    private static string ConfigPath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");

    public static AppConfig Load()
    {
        if (!File.Exists(ConfigPath))
        {
            var defaults = new AppConfig();
            defaults.Save();
            return defaults;
        }

        var json = File.ReadAllText(ConfigPath);
        return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
    }

    public void Save()
    {
        File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(this, Formatting.Indented));
    }
}
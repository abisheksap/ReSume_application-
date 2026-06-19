using System.IO;
using System.Text.Json;

namespace ReSume.Core.Configuration;

public class AppSettings {
    public bool StartWithWindows { get; set; } = true;
    public int MaxSessionCount { get; set; } = 50;
    public long MaxStorageBytes { get; set; } = 200 * 1024 * 1024;
    public int SaveTimeoutSeconds { get; set; } = 30;
    public bool MinimizeToTray { get; set; } = true;
    public string? ChromeExtensionId { get; set; }
    public string? EdgeExtensionId { get; set; }

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ReSume", "settings", "config.json");

    public static AppSettings Load() {
        if (File.Exists(SettingsPath))
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath)) ?? new AppSettings();
        return new AppSettings();
    }

    public void Save() {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
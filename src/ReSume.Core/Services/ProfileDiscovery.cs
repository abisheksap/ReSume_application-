using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ReSume.Core.Models;

namespace ReSume.Core.Services;

public class ProfileDiscovery {
    public static List<BrowserProfile> DiscoverChromeProfiles() {
        return DiscoverProfiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Google\Chrome\User Data"));
    }
    public static List<BrowserProfile> DiscoverEdgeProfiles() {
        return DiscoverProfiles(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\Edge\User Data"));
    }
    private static List<BrowserProfile> DiscoverProfiles(string userDataDir) {
        var profiles = new List<BrowserProfile>();
        if (!Directory.Exists(userDataDir)) return profiles;
        foreach (string dir in Directory.GetDirectories(userDataDir)) {
            string dirName = Path.GetFileName(dir);
            if (dirName.StartsWith("Profile ") || dirName == "Default") {
                string profileName = dirName;
                string prefsFile = Path.Combine(dir, "Preferences");
                if (File.Exists(prefsFile)) {
                    try {
                        var json = JsonDocument.Parse(File.ReadAllText(prefsFile));
                        if (json.RootElement.TryGetProperty("profile", out var prof) &&
                            prof.TryGetProperty("name", out var name))
                            profileName = name.GetString() ?? dirName;
                    } catch { }
                }
                profiles.Add(new BrowserProfile { ProfileDirectory = dirName, ProfileName = profileName, ProfileId = dirName });
            }
        }
        return profiles;
    }
}
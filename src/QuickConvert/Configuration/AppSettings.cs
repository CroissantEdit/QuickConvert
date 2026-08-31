using System.IO;
using System.Text.Json;

namespace QuickConvert.Configuration;

public sealed class UserSettings
{
    public bool ShowSuccessNotifications { get; set; }
}

public static class SettingsStore
{
    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QuickConvert",
        "settings.json");

    public static UserSettings Load()
    {
        try
        {
            var path = SettingsPath;
            if (!File.Exists(path)) return new UserSettings();
            return JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path)) ?? new UserSettings();
        }
        catch
        {
            return new UserSettings();
        }
    }

    public static void Save(UserSettings settings)
    {
        try
        {
            var path = SettingsPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Settings are optional. Conversion should still work if the profile is read-only.
        }
    }
}

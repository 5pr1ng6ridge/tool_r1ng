using System.IO;
using System.Text.Json;

namespace tool_r1ng.Core;

public sealed class LauncherSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "tool_r1ng");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public bool EnableEverythingFileSearch { get; set; }

    public bool EnableEverythingAppSearch { get; set; }

    public int Version { get; set; }

    public static LauncherSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new LauncherSettings();
            }

            return JsonSerializer.Deserialize<LauncherSettings>(
                File.ReadAllText(SettingsPath),
                new JsonSerializerOptions
                {
                    IncludeFields = false
                }) ?? new LauncherSettings();
        }
        catch
        {
            return new LauncherSettings();
        }
    }

    public void SetEverythingFileSearch(bool isEnabled)
    {
        if (EnableEverythingFileSearch == isEnabled)
        {
            return;
        }

        EnableEverythingFileSearch = isEnabled;
        Save();
    }

    public void SetEverythingAppSearch(bool isEnabled)
    {
        if (EnableEverythingAppSearch == isEnabled)
        {
            return;
        }

        EnableEverythingAppSearch = isEnabled;
        Save();
    }

    private void Save()
    {
        Version++;
        Directory.CreateDirectory(SettingsDirectory);
        File.WriteAllText(
            SettingsPath,
            JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}

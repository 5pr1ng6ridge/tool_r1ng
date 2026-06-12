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

    public bool EnableAcrylicBackdrop { get; set; } = true;

    public double AcrylicOpacity { get; set; } = 0.7;

    public int Version { get; set; }

    public static LauncherSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                return new LauncherSettings();
            }

            var settings = JsonSerializer.Deserialize<LauncherSettings>(
                File.ReadAllText(SettingsPath),
                new JsonSerializerOptions
                {
                    IncludeFields = false
                }) ?? new LauncherSettings();
            settings.AcrylicOpacity = NormalizeAcrylicOpacity(settings.AcrylicOpacity);
            return settings;
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

    public void SetAcrylicBackdrop(bool isEnabled)
    {
        if (EnableAcrylicBackdrop == isEnabled)
        {
            return;
        }

        EnableAcrylicBackdrop = isEnabled;
        Save();
    }

    public void SetAcrylicOpacity(double opacity)
    {
        var normalizedOpacity = NormalizeAcrylicOpacity(opacity);
        if (Math.Abs(AcrylicOpacity - normalizedOpacity) < 0.001)
        {
            return;
        }

        AcrylicOpacity = normalizedOpacity;
        Save();
    }

    private static double NormalizeAcrylicOpacity(double opacity)
    {
        return Math.Clamp(opacity, 0.05, 0.90);
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

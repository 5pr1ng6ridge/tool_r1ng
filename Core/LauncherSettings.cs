using System.IO;
using System.Globalization;
using System.Text.Json;

namespace tool_r1ng.Core;

public sealed class LauncherSettings
{
    public const string DefaultWindowMaterialColor = "#FFFFFF";
    public const string DefaultHeaderMaterialColor = "#FFFFFF";
    public const string DefaultSettingsMaterialColor = "#FFFFFF";
    public const string DefaultBorderMaterialColor = "#D6D0C3";

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "tool_r1ng");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public bool EnableEverythingFileSearch { get; set; }

    public bool EnableEverythingAppSearch { get; set; }

    public bool EnableAcrylicBackdrop { get; set; } = true;

    public double AcrylicOpacity { get; set; } = 0.7;

    public string WindowMaterialColor { get; set; } = DefaultWindowMaterialColor;

    public string HeaderMaterialColor { get; set; } = DefaultHeaderMaterialColor;

    public string SettingsMaterialColor { get; set; } = DefaultSettingsMaterialColor;

    public string BorderMaterialColor { get; set; } = DefaultBorderMaterialColor;

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
            settings.WindowMaterialColor = NormalizeHexColor(settings.WindowMaterialColor, DefaultWindowMaterialColor);
            settings.HeaderMaterialColor = NormalizeHexColor(settings.HeaderMaterialColor, DefaultHeaderMaterialColor);
            settings.SettingsMaterialColor = NormalizeHexColor(settings.SettingsMaterialColor, DefaultSettingsMaterialColor);
            settings.BorderMaterialColor = NormalizeHexColor(settings.BorderMaterialColor, DefaultBorderMaterialColor);
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

    public void SetWindowMaterialColor(string color)
    {
        var normalizedColor = NormalizeHexColor(color, DefaultWindowMaterialColor);
        if (WindowMaterialColor == normalizedColor)
        {
            return;
        }

        WindowMaterialColor = normalizedColor;
        Save();
    }

    public void SetHeaderMaterialColor(string color)
    {
        var normalizedColor = NormalizeHexColor(color, DefaultHeaderMaterialColor);
        if (HeaderMaterialColor == normalizedColor)
        {
            return;
        }

        HeaderMaterialColor = normalizedColor;
        Save();
    }

    public void SetSettingsMaterialColor(string color)
    {
        var normalizedColor = NormalizeHexColor(color, DefaultSettingsMaterialColor);
        if (SettingsMaterialColor == normalizedColor)
        {
            return;
        }

        SettingsMaterialColor = normalizedColor;
        Save();
    }

    public void SetBorderMaterialColor(string color)
    {
        var normalizedColor = NormalizeHexColor(color, DefaultBorderMaterialColor);
        if (BorderMaterialColor == normalizedColor)
        {
            return;
        }

        BorderMaterialColor = normalizedColor;
        Save();
    }

    public void ResetMaterialColors()
    {
        var isChanged = WindowMaterialColor != DefaultWindowMaterialColor
            || HeaderMaterialColor != DefaultHeaderMaterialColor
            || SettingsMaterialColor != DefaultSettingsMaterialColor
            || BorderMaterialColor != DefaultBorderMaterialColor;

        if (!isChanged)
        {
            return;
        }

        WindowMaterialColor = DefaultWindowMaterialColor;
        HeaderMaterialColor = DefaultHeaderMaterialColor;
        SettingsMaterialColor = DefaultSettingsMaterialColor;
        BorderMaterialColor = DefaultBorderMaterialColor;
        Save();
    }

    private static double NormalizeAcrylicOpacity(double opacity)
    {
        return Math.Clamp(opacity, 0.05, 0.90);
    }

    public static string NormalizeHexColor(string? color, string fallback)
    {
        return TryNormalizeHexColor(color, out var normalizedColor)
            ? normalizedColor
            : fallback;
    }

    public static bool TryNormalizeHexColor(string? color, out string normalizedColor)
    {
        normalizedColor = string.Empty;
        if (string.IsNullOrWhiteSpace(color))
        {
            return false;
        }

        var value = color.Trim();
        if (value.StartsWith('#'))
        {
            value = value[1..];
        }

        if (value.Length == 3)
        {
            value = string.Concat(value.Select(character => new string(character, 2)));
        }

        if (value.Length != 6
            || !int.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _))
        {
            return false;
        }

        normalizedColor = $"#{value.ToUpperInvariant()}";
        return true;
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

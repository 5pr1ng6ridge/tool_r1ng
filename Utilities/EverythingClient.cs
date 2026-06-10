using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace tool_r1ng.Utilities;

public static class EverythingClient
{
    private const uint EverythingRequestFileName = 0x00000001;
    private const uint EverythingRequestPath = 0x00000002;
    private const int EverythingSortNameAscending = 1;
    private const int EverythingSortNameDescending = 2;
    private const int EverythingSortPathAscending = 3;
    private const int EverythingSortPathDescending = 4;
    private const int EverythingSortSizeAscending = 5;
    private const int EverythingSortSizeDescending = 6;
    private const int EverythingSortExtensionAscending = 7;
    private const int EverythingSortExtensionDescending = 8;
    private const int EverythingSortTypeNameAscending = 9;
    private const int EverythingSortTypeNameDescending = 10;
    private const int EverythingSortDateCreatedAscending = 11;
    private const int EverythingSortDateCreatedDescending = 12;
    private const int EverythingSortDateModifiedAscending = 13;
    private const int EverythingSortDateModifiedDescending = 14;
    private const int EverythingSortAttributesAscending = 15;
    private const int EverythingSortAttributesDescending = 16;
    private const int EverythingSortFileListNameAscending = 17;
    private const int EverythingSortFileListNameDescending = 18;
    private const int EverythingSortRunCountAscending = 19;
    private const int EverythingSortRunCountDescending = 20;
    private const int EverythingSortDateRecentlyChangedAscending = 21;
    private const int EverythingSortDateRecentlyChangedDescending = 22;
    private const int EverythingSortDateAccessedAscending = 23;
    private const int EverythingSortDateAccessedDescending = 24;
    private const int EverythingSortDateRunAscending = 25;
    private const int EverythingSortDateRunDescending = 26;

    private static readonly object QueryLock = new();
    private static readonly object SettingsLock = new();
    private static EverythingSearchOptions? cachedSearchOptions;
    private static string? cachedSettingsPath;
    private static DateTime cachedSettingsLastWriteUtc;

    public static IReadOnlyList<EverythingSearchResult> Search(
        string query,
        int maxResults,
        CancellationToken cancellationToken,
        EverythingSearchProfile profile = EverythingSearchProfile.Stable)
    {
        if (string.IsNullOrWhiteSpace(query) || maxResults <= 0)
        {
            return [];
        }

        lock (QueryLock)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var searchOptions = profile == EverythingSearchProfile.UserSettings
                ? LoadSearchOptions()
                : EverythingSearchOptions.Stable;

            Everything_SetSearchW(query);
            Everything_SetMatchPath(searchOptions.MatchPath);
            Everything_SetMatchCase(searchOptions.MatchCase);
            Everything_SetMatchWholeWord(searchOptions.MatchWholeWord);
            Everything_SetRegex(searchOptions.Regex);
            Everything_SetMax(maxResults);
            Everything_SetOffset(0);
            Everything_SetSort(searchOptions.Sort);
            Everything_SetRequestFlags(EverythingRequestFileName | EverythingRequestPath);

            if (!Everything_QueryW(true))
            {
                throw new EverythingUnavailableException(GetLastErrorMessage());
            }

            var results = new List<EverythingSearchResult>();
            var count = Math.Min(maxResults, (int)Everything_GetNumResults());
            for (uint index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var fullPath = GetFullPath(index);
                if (!string.IsNullOrWhiteSpace(fullPath))
                {
                    results.Add(new EverythingSearchResult(fullPath, Everything_IsFolderResult(index)));
                }
            }

            return results;
        }
    }

    public static bool IsAvailable()
    {
        return TryCheckAvailability(out _);
    }

    public static bool TryCheckAvailability(out string message)
    {
        try
        {
            _ = Search("test", 1, CancellationToken.None);
            message = "Everything is ready";
            return true;
        }
        catch (DllNotFoundException)
        {
            message = "Everything64.dll was not found beside the app executable";
            return false;
        }
        catch (BadImageFormatException)
        {
            message = "Everything64.dll architecture does not match this app";
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            message = "Everything64.dll is not a compatible SDK DLL";
            return false;
        }
        catch (EverythingUnavailableException exception)
        {
            message = exception.Message;
            return false;
        }
        catch
        {
            message = "Everything is not available";
            return false;
        }
    }

    private static string GetLastErrorMessage()
    {
        return Everything_GetLastError() switch
        {
            2 => "Everything IPC is unavailable. Start Everything or enable Everything Service",
            6 => "Everything returned an invalid result index",
            7 => "Everything SDK was called before a query completed",
            var error => $"Everything query failed with SDK error {error}"
        };
    }

    private static string GetFullPath(uint index)
    {
        var buffer = new StringBuilder(32768);
        _ = Everything_GetResultFullPathNameW(index, buffer, (uint)buffer.Capacity);
        return buffer.ToString();
    }

    private static EverythingSearchOptions LoadSearchOptions()
    {
        lock (SettingsLock)
        {
            var settingsPath = FindSettingsPath();
            if (settingsPath is null || !File.Exists(settingsPath))
            {
                cachedSearchOptions = EverythingSearchOptions.UserDefault;
                cachedSettingsPath = null;
                cachedSettingsLastWriteUtc = DateTime.MinValue;
                return cachedSearchOptions;
            }

            var lastWriteUtc = File.GetLastWriteTimeUtc(settingsPath);
            if (cachedSearchOptions is not null
                && string.Equals(cachedSettingsPath, settingsPath, StringComparison.OrdinalIgnoreCase)
                && cachedSettingsLastWriteUtc == lastWriteUtc)
            {
                return cachedSearchOptions;
            }

            var values = ReadSettingsValues(settingsPath);
            cachedSearchOptions = new EverythingSearchOptions(
                ReadBoolean(values, "match_path", "home_match_path"),
                ReadBoolean(values, "match_case", "home_match_case"),
                ReadBoolean(values, "match_whole_word", "home_match_whole_word"),
                ReadBoolean(values, "match_regex", "home_regex"),
                ReadSort(values));
            cachedSettingsPath = settingsPath;
            cachedSettingsLastWriteUtc = lastWriteUtc;
            return cachedSearchOptions;
        }
    }

    private static string? FindSettingsPath()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Everything"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Everything"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything")
        };

        foreach (var root in roots.Where(path => Path.IsPathRooted(path) && Directory.Exists(path)))
        {
            var exactPath = Path.Combine(root, "Everything.ini");
            if (File.Exists(exactPath))
            {
                return exactPath;
            }

            var instancePath = Directory
                .EnumerateFiles(root, "Everything*.ini", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (instancePath is not null)
            {
                return instancePath;
            }
        }

        return null;
    }

    private static Dictionary<string, string> ReadSettingsValues(string settingsPath)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (var line in File.ReadLines(settingsPath))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0 || trimmed.StartsWith(';') || trimmed.StartsWith('['))
                {
                    continue;
                }

                var separatorIndex = trimmed.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = trimmed[..separatorIndex].Trim();
                var value = trimmed[(separatorIndex + 1)..].Trim();
                values[key] = value;
            }
        }
        catch
        {
            return [];
        }

        return values;
    }

    private static bool ReadBoolean(IReadOnlyDictionary<string, string> values, string currentKey, string fallbackKey)
    {
        if (TryReadBoolean(values, currentKey, out var currentValue))
        {
            return currentValue;
        }

        return TryReadBoolean(values, fallbackKey, out var fallbackValue) && fallbackValue;
    }

    private static bool TryReadBoolean(IReadOnlyDictionary<string, string> values, string key, out bool value)
    {
        value = false;
        if (!values.TryGetValue(key, out var rawValue))
        {
            return false;
        }

        if (rawValue == "1")
        {
            value = true;
            return true;
        }

        if (rawValue == "0")
        {
            value = false;
            return true;
        }

        return bool.TryParse(rawValue, out value);
    }

    private static int ReadSort(IReadOnlyDictionary<string, string> values)
    {
        if (!values.TryGetValue("sort", out var sort) || string.IsNullOrWhiteSpace(sort))
        {
            return EverythingSortNameAscending;
        }

        var ascending = !TryReadBoolean(values, "sort_ascending", out var isAscending) || isAscending;
        return NormalizeSortName(sort) switch
        {
            "name" => ascending ? EverythingSortNameAscending : EverythingSortNameDescending,
            "path" => ascending ? EverythingSortPathAscending : EverythingSortPathDescending,
            "size" => ascending ? EverythingSortSizeAscending : EverythingSortSizeDescending,
            "extension" => ascending ? EverythingSortExtensionAscending : EverythingSortExtensionDescending,
            "typename" => ascending ? EverythingSortTypeNameAscending : EverythingSortTypeNameDescending,
            "datecreated" => ascending ? EverythingSortDateCreatedAscending : EverythingSortDateCreatedDescending,
            "datemodified" => ascending ? EverythingSortDateModifiedAscending : EverythingSortDateModifiedDescending,
            "attributes" => ascending ? EverythingSortAttributesAscending : EverythingSortAttributesDescending,
            "filelistfilename" => ascending ? EverythingSortFileListNameAscending : EverythingSortFileListNameDescending,
            "runcount" => ascending ? EverythingSortRunCountAscending : EverythingSortRunCountDescending,
            "daterecentlychanged" => ascending ? EverythingSortDateRecentlyChangedAscending : EverythingSortDateRecentlyChangedDescending,
            "dateaccessed" => ascending ? EverythingSortDateAccessedAscending : EverythingSortDateAccessedDescending,
            "daterun" => ascending ? EverythingSortDateRunAscending : EverythingSortDateRunDescending,
            _ => EverythingSortNameAscending
        };
    }

    private static string NormalizeSortName(string sort)
    {
        var builder = new StringBuilder(sort.Length);
        foreach (var character in sort)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString();
    }

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern void Everything_SetSearchW(string search);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetMatchPath(bool enable);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetMatchCase(bool enable);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetMatchWholeWord(bool enable);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetRegex(bool enable);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetMax(int max);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetOffset(int offset);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetSort(int sortType);

    [DllImport("Everything64.dll")]
    private static extern void Everything_SetRequestFlags(uint requestFlags);

    [DllImport("Everything64.dll")]
    private static extern bool Everything_QueryW(bool wait);

    [DllImport("Everything64.dll")]
    private static extern uint Everything_GetLastError();

    [DllImport("Everything64.dll")]
    private static extern uint Everything_GetNumResults();

    [DllImport("Everything64.dll", CharSet = CharSet.Unicode)]
    private static extern uint Everything_GetResultFullPathNameW(uint index, StringBuilder buffer, uint bufferSize);

    [DllImport("Everything64.dll")]
    private static extern bool Everything_IsFolderResult(uint index);
}

public sealed record EverythingSearchResult(string FullPath, bool IsFolder);

internal sealed record EverythingSearchOptions(
    bool MatchPath,
    bool MatchCase,
    bool MatchWholeWord,
    bool Regex,
    int Sort)
{
    public static EverythingSearchOptions Stable { get; } = new(true, false, false, false, 1);
    public static EverythingSearchOptions UserDefault { get; } = new(false, false, false, false, 1);
}

public enum EverythingSearchProfile
{
    Stable,
    UserSettings
}

public sealed class EverythingUnavailableException : Exception
{
    public EverythingUnavailableException(string message)
        : base(message)
    {
    }
}

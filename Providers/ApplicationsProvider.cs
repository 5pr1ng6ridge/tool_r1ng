using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.Win32;
using tool_r1ng.Core;
using tool_r1ng.Utilities;

namespace tool_r1ng.Providers;

public sealed class ApplicationsProvider : tool_r1ng.Core.IQueryProvider
{
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private IReadOnlyList<ApplicationEntry>? _cachedApps;

    public string Id => "applications";

    public string Name => "Apps";

    public async ValueTask<IReadOnlyList<QueryResult>> QueryAsync(QueryContext context, CancellationToken cancellationToken)
    {
        var apps = await EnsureCacheAsync(cancellationToken);

        if (context.IsEmpty)
        {
            return Array.Empty<QueryResult>();
        }

        return apps
            .Select(app => new
            {
                App = app,
                NameMatch = FuzzyMatcher.Match(app.Name, context.Query),
                SearchTextScore = FuzzyMatcher.Score(app.SearchText, context.Query) * 0.75
            })
            .Select(item => new
            {
                item.App,
                item.NameMatch,
                Score = item.App.Priority + Math.Max(item.NameMatch.Score, item.SearchTextScore)
            })
            .Where(item => item.Score >= 28)
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.App.Name)
            .Take(8)
            .Select(item => CreateResult(item.App, item.Score, item.NameMatch.MatchedIndices))
            .ToList();
    }

    private QueryResult CreateResult(ApplicationEntry app, double score, IReadOnlyList<int> matchedTitleIndices)
    {
        return new QueryResult
        {
            Title = app.Name,
            HighlightedTitle = HighlightBuilder.Build(app.Name, matchedTitleIndices),
            Subtitle = app.Location,
            IconGlyph = "\uECAA",
            IconImage = IconLoader.LoadAssociatedIcon(app.IconPath)
                ?? IconLoader.LoadAssociatedIcon(app.LaunchPath),
            ProviderId = Id,
            ProviderName = Name,
            Score = score,
            ExecuteAsync = _ => ProcessLauncher.OpenAsync(app.LaunchPath),
            SecondaryActionToolTip = "Open containing folder",
            SecondaryActionAsync = string.IsNullOrWhiteSpace(app.FolderPath)
                ? null
                : _ => ProcessLauncher.OpenContainingFolderAsync(app.FolderPath)
        };
    }

    private async Task<IReadOnlyList<ApplicationEntry>> EnsureCacheAsync(CancellationToken cancellationToken)
    {
        if (_cachedApps is not null)
        {
            return _cachedApps;
        }

        await _cacheLock.WaitAsync(cancellationToken);
        try
        {
            if (_cachedApps is not null)
            {
                return _cachedApps;
            }

            _cachedApps = await Task.Run(LoadApplications, cancellationToken);
            return _cachedApps;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private static IReadOnlyList<ApplicationEntry> LoadApplications()
    {
        var entries = new List<ApplicationEntry>();
        AddEntries(entries, LoadShortcutApplications);
        AddEntries(entries, LoadShellAppsFolderApplications);
        AddEntries(entries, LoadWindowsAppPathAliasApplications);

        return entries
            .GroupBy(app => app.LaunchPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(app => app.Priority).First())
            .GroupBy(app => app.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(app => app.Priority)
                .ThenBy(app => app.LaunchPath.Length)
                .First())
            .OrderBy(app => app.Name)
            .ToList();
    }

    private static void AddEntries(
        ICollection<ApplicationEntry> entries,
        Func<IEnumerable<ApplicationEntry>> sourceFactory)
    {
        try
        {
            foreach (var entry in sourceFactory())
            {
                entries.Add(entry);
            }
        }
        catch
        {
        }
    }

    private static IEnumerable<ApplicationEntry> LoadShortcutApplications()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)
        };

        foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var file in EnumerateLaunchableFiles(root))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                yield return new ApplicationEntry(
                    name,
                    file,
                    file,
                    file,
                    GetRelativeLocation(root, file),
                    24);
            }
        }
    }

    private static IEnumerable<ApplicationEntry> LoadShellAppsFolderApplications()
    {
        object? shell = null;
        object? folder = null;
        object? items = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
            {
                yield break;
            }

            shell = Activator.CreateInstance(shellType);
            folder = shellType.InvokeMember(
                "NameSpace",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                shell,
                ["shell:AppsFolder"]);

            if (folder is null)
            {
                yield break;
            }

            items = folder.GetType().InvokeMember(
                "Items",
                System.Reflection.BindingFlags.InvokeMethod,
                null,
                folder,
                null);

            if (items is not System.Collections.IEnumerable enumerableItems)
            {
                yield break;
            }

            foreach (var item in enumerableItems)
            {
                var name = GetComString(item, "Name");
                var path = GetComString(item, "Path");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                yield return new ApplicationEntry(
                    name,
                    path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase)
                        ? path
                        : $"shell:AppsFolder\\{path}",
                    string.Empty,
                    string.Empty,
                    "Windows apps",
                    20);
            }
        }
        finally
        {
            ReleaseComObject(items);
            ReleaseComObject(folder);
            ReleaseComObject(shell);
        }
    }

    private static IEnumerable<ApplicationEntry> LoadWindowsAppPathAliasApplications()
    {
        var pathDirectories = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var directory in pathDirectories)
        {
            if (!directory.Contains(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase)
                && !directory.EndsWith(@"\WindowsApps", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var file in EnumerateExecutables(directory, maxDepth: 0))
            {
                var packageDirectory = FindAppxPackageDirectory(file);
                if (packageDirectory is null)
                {
                    continue;
                }

                foreach (var entry in TryLoadAppxPackageApplications(packageDirectory))
                {
                    yield return entry with { Priority = Math.Max(entry.Priority, 22) };
                }
            }
        }
    }

    private static string? FindAppxPackageDirectory(string file)
    {
        var directory = Path.GetDirectoryName(file);
        while (!string.IsNullOrWhiteSpace(directory))
        {
            if (File.Exists(Path.Combine(directory, "AppxManifest.xml")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    private static string? GetComString(object? value, string propertyName)
    {
        try
        {
            return value?.GetType().InvokeMember(
                propertyName,
                System.Reflection.BindingFlags.GetProperty,
                null,
                value,
                null) as string;
        }
        catch
        {
            return null;
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private static IEnumerable<ApplicationEntry> LoadRegisteredApplications()
    {
        const string uninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";

        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            {
                RegistryKey? baseKey = null;
                RegistryKey? uninstallKey = null;

                try
                {
                    baseKey = RegistryKey.OpenBaseKey(hive, view);
                    uninstallKey = baseKey.OpenSubKey(uninstallPath);
                }
                catch
                {
                    uninstallKey?.Dispose();
                    baseKey?.Dispose();
                    continue;
                }

                using (baseKey)
                using (uninstallKey)
                {
                    if (uninstallKey is null)
                    {
                        continue;
                    }

                    string[] subKeyNames;
                    try
                    {
                        subKeyNames = uninstallKey.GetSubKeyNames();
                    }
                    catch
                    {
                        continue;
                    }

                    foreach (var subKeyName in subKeyNames)
                    {
                        using var appKey = SafeOpenSubKey(uninstallKey, subKeyName);
                        if (appKey is null)
                        {
                            continue;
                        }

                        var displayName = SafeGetString(appKey, "DisplayName");
                        if (string.IsNullOrWhiteSpace(displayName)
                            || IsSystemComponent(appKey)
                            || LooksLikeMaintenanceTool(displayName))
                        {
                            continue;
                        }

                        var launchPath = FindRegisteredLaunchPath(appKey);
                        if (string.IsNullOrWhiteSpace(launchPath))
                        {
                            continue;
                        }

                        yield return new ApplicationEntry(
                            displayName.Trim(),
                            launchPath,
                            launchPath,
                            launchPath,
                            "Installed application",
                            10);
                    }
                }
            }
        }
    }

    private static IEnumerable<ApplicationEntry> LoadProgramFileApplications()
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "WinGet",
                "Packages")
        };

        foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var file in EnumerateExecutables(root, maxDepth: 3))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(name) || LooksLikeMaintenanceTool(name))
                {
                    continue;
                }

                yield return new ApplicationEntry(
                    InferApplicationName(root, file, name),
                    file,
                    file,
                    file,
                    GetRelativeLocation(root, file),
                    0);
            }
        }
    }

    private static IEnumerable<ApplicationEntry> LoadAppxPackageApplications()
    {
        var roots = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps"),
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "WindowsApps")
        };

        foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var packageDirectory in EnumerateDirectoriesSafely(root))
            {
                foreach (var entry in TryLoadAppxPackageApplications(packageDirectory))
                {
                    yield return entry;
                }
            }
        }
    }

    private static IEnumerable<ApplicationEntry> LoadPackagedApplications()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var roots = new[]
        {
            Path.Combine(localAppData, "OpenAI"),
            Path.Combine(localAppData, "Codex"),
            Path.Combine(localAppData, "Anthropic"),
            Path.Combine(localAppData, "Cursor"),
            Path.Combine(localAppData, "Microsoft", "WindowsApps")
        };

        foreach (var root in roots.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var file in EnumerateExecutables(root, maxDepth: 4))
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                if (string.IsNullOrWhiteSpace(fileName) || LooksLikeMaintenanceTool(fileName))
                {
                    continue;
                }

                yield return new ApplicationEntry(
                    InferApplicationName(root, file, fileName),
                    file,
                    file,
                    file,
                    GetRelativeLocation(root, file),
                    4);
            }
        }
    }

    private static IEnumerable<string> EnumerateLaunchableFiles(string root)
    {
        return EnumerateFilesSafely(
            root,
            file =>
            {
                var extension = Path.GetExtension(file);
                return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".appref-ms", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".url", StringComparison.OrdinalIgnoreCase);
            });
    }

    private static IEnumerable<string> EnumerateExecutables(string root, int maxDepth)
    {
        var pending = new Queue<(string Directory, int Depth)>();
        pending.Enqueue((root, 0));

        while (pending.Count > 0)
        {
            var (directory, depth) = pending.Dequeue();

            string[] files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*.exe", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch
            {
                files = [];
            }

            foreach (var file in files)
            {
                yield return file;
            }

            if (depth >= maxDepth)
            {
                continue;
            }

            string[] childDirectories;
            try
            {
                childDirectories = Directory.EnumerateDirectories(directory).ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                pending.Enqueue((childDirectory, depth + 1));
            }
        }
    }

    private static IEnumerable<string> EnumerateFilesSafely(string root, Func<string, bool> predicate)
    {
        var pending = new Queue<string>();
        pending.Enqueue(root);

        while (pending.Count > 0)
        {
            var directory = pending.Dequeue();

            string[] files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly).ToArray();
            }
            catch
            {
                files = [];
            }

            foreach (var file in files)
            {
                if (predicate(file))
                {
                    yield return file;
                }
            }

            string[] childDirectories;
            try
            {
                childDirectories = Directory.EnumerateDirectories(directory).ToArray();
            }
            catch
            {
                continue;
            }

            foreach (var childDirectory in childDirectories)
            {
                pending.Enqueue(childDirectory);
            }
        }
    }

    private static IEnumerable<string> EnumerateDirectoriesSafely(string root)
    {
        string[] directories;
        try
        {
            directories = Directory.EnumerateDirectories(root).ToArray();
        }
        catch
        {
            yield break;
        }

        foreach (var directory in directories)
        {
            yield return directory;
        }
    }

    private static IEnumerable<ApplicationEntry> TryLoadAppxPackageApplications(string packageDirectory)
    {
        var manifestPath = Path.Combine(packageDirectory, "AppxManifest.xml");
        if (!File.Exists(manifestPath))
        {
            yield break;
        }

        XDocument document;
        try
        {
            document = XDocument.Load(manifestPath);
        }
        catch
        {
            yield break;
        }

        var identity = document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Identity");
        var identityName = identity?.Attribute("Name")?.Value;
        var publisherId = ExtractPackagePublisherId(packageDirectory);

        if (string.IsNullOrWhiteSpace(identityName) || string.IsNullOrWhiteSpace(publisherId))
        {
            yield break;
        }

        var packageDisplayName = GetElementText(document, "Properties", "DisplayName")
            ?? CleanApplicationName(identityName);
        var packageFamilyName = $"{identityName}_{publisherId}";

        foreach (var application in document.Descendants().Where(element => element.Name.LocalName == "Application"))
        {
            var applicationId = application.Attribute("Id")?.Value;
            if (string.IsNullOrWhiteSpace(applicationId))
            {
                continue;
            }

            var visualElements = application.Elements().FirstOrDefault(element => element.Name.LocalName == "VisualElements");
            var displayName = visualElements?.Attribute("DisplayName")?.Value;
            var logo = visualElements?.Attribute("Square44x44Logo")?.Value
                ?? visualElements?.Attribute("Square150x150Logo")?.Value
                ?? GetElementText(document, "Properties", "Logo");
            var executablePath = Path.Combine(packageDirectory, application.Attribute("Executable")?.Value ?? string.Empty);
            var iconPath = ResolvePackageAssetPath(packageDirectory, logo) ?? executablePath;

            yield return new ApplicationEntry(
                CleanPackageResourceName(displayName) ?? CleanPackageResourceName(packageDisplayName) ?? CleanApplicationName(identityName),
                $"shell:AppsFolder\\{packageFamilyName}!{applicationId}",
                iconPath,
                packageDirectory,
                "Packaged application",
                18);
        }
    }

    private static string? ExtractPackagePublisherId(string packageDirectory)
    {
        var name = Path.GetFileName(packageDirectory);
        var index = name.LastIndexOf("__", StringComparison.Ordinal);
        return index >= 0 && index + 2 < name.Length
            ? name[(index + 2)..]
            : null;
    }

    private static string? GetElementText(XDocument document, string parentName, string childName)
    {
        return document
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == parentName)?
            .Elements()
            .FirstOrDefault(element => element.Name.LocalName == childName)?
            .Value;
    }

    private static string? ResolvePackageAssetPath(string packageDirectory, string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = Path.Combine(packageDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path);
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);

        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return null;
        }

        try
        {
            var candidates = Directory
                .EnumerateFiles(directory, $"{name}*.{extension.TrimStart('.')}", SearchOption.TopDirectoryOnly)
                .ToArray();

            return candidates
                .OrderByDescending(GetPackageIconScore)
                .ThenBy(file => file.Length)
                .FirstOrDefault()
                ?? (File.Exists(path) ? path : null);
        }
        catch
        {
            return File.Exists(path) ? path : null;
        }
    }

    private static int GetPackageIconScore(string path)
    {
        var name = Path.GetFileName(path).ToLowerInvariant();
        var score = 0;

        if (name.Contains("unplated"))
        {
            score += 100;
        }

        if (name.Contains("targetsize-44") || name.Contains("targetsize-48") || name.Contains("targetsize-40"))
        {
            score += 80;
        }
        else if (name.Contains("targetsize-32") || name.Contains("targetsize-64"))
        {
            score += 60;
        }
        else if (name.Contains("scale-200"))
        {
            score += 20;
        }

        return score;
    }

    private static string? CleanPackageResourceName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return name.Trim();
    }

    private static string? FindRegisteredLaunchPath(RegistryKey appKey)
    {
        var displayIcon = SafeGetString(appKey, "DisplayIcon");
        var iconPath = ExtractExecutablePath(displayIcon);
        if (IsLaunchableExecutable(iconPath))
        {
            return iconPath;
        }

        var installLocation = SafeGetString(appKey, "InstallLocation");
        if (!string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation))
        {
            var folderName = Path.GetFileName(installLocation.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            string? preferredExe = null;
            try
            {
                preferredExe = Directory
                    .EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly)
                    .Where(file => !LooksLikeMaintenanceTool(Path.GetFileNameWithoutExtension(file)))
                    .OrderByDescending(file => FuzzyMatcher.Score(Path.GetFileNameWithoutExtension(file), folderName))
                    .FirstOrDefault();
            }
            catch
            {
            }

            if (IsLaunchableExecutable(preferredExe))
            {
                return preferredExe;
            }
        }

        return null;
    }

    private static string? ExtractExecutablePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = Environment.ExpandEnvironmentVariables(value.Trim());
        if (trimmed.StartsWith('"'))
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            return closingQuote > 1
                ? trimmed[1..closingQuote]
                : null;
        }

        var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exeIndex >= 0
            ? trimmed[..(exeIndex + 4)]
            : trimmed.Split(',')[0].Trim();
    }

    private static bool IsLaunchableExecutable(string? path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase)
            && File.Exists(path)
            && !LooksLikeMaintenanceTool(Path.GetFileNameWithoutExtension(path));
    }

    private static bool IsSystemComponent(RegistryKey appKey)
    {
        try
        {
            return appKey.GetValue("SystemComponent") is int value && value == 1;
        }
        catch
        {
            return false;
        }
    }

    private static RegistryKey? SafeOpenSubKey(RegistryKey key, string name)
    {
        try
        {
            return key.OpenSubKey(name);
        }
        catch
        {
            return null;
        }
    }

    private static string? SafeGetString(RegistryKey key, string name)
    {
        try
        {
            return key.GetValue(name) as string;
        }
        catch
        {
            return null;
        }
    }

    private static bool LooksLikeMaintenanceTool(string name)
    {
        var normalized = name.ToLowerInvariant();
        return normalized.Contains("uninstall")
            || normalized.Contains("unins")
            || normalized.Contains("setup")
            || normalized.Contains("install")
            || normalized.Contains("update")
            || normalized.Contains("crash")
            || normalized.Contains("helper")
            || normalized.Contains("service");
    }

    private static string InferApplicationName(string root, string file, string fallbackName)
    {
        var normalizedFile = Path.GetFileNameWithoutExtension(file);
        var relativeDirectory = Path.GetDirectoryName(Path.GetRelativePath(root, file)) ?? string.Empty;
        var parts = relativeDirectory
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        if (parts.Any(part => part.Equals("Codex", StringComparison.OrdinalIgnoreCase))
            && normalizedFile.Equals("codex", StringComparison.OrdinalIgnoreCase))
        {
            return "Codex";
        }

        if (parts.Any(part => part.Equals("OpenAI", StringComparison.OrdinalIgnoreCase))
            && normalizedFile.Equals("codex", StringComparison.OrdinalIgnoreCase))
        {
            return "Codex";
        }

        var namedPart = parts
            .Reverse()
            .FirstOrDefault(part =>
                !part.Equals("bin", StringComparison.OrdinalIgnoreCase)
                && !part.Equals("app", StringComparison.OrdinalIgnoreCase)
                && !LooksLikeVersionOrHash(part));

        return string.IsNullOrWhiteSpace(namedPart)
            ? fallbackName
            : CleanApplicationName(namedPart);
    }

    private static string CleanApplicationName(string name)
    {
        var underscoreIndex = name.IndexOf('_');
        return underscoreIndex > 0
            ? name[..underscoreIndex]
            : name;
    }

    private static bool LooksLikeVersionOrHash(string value)
    {
        return value.Any(char.IsDigit)
            && (value.All(character => char.IsAsciiHexDigit(character) || character is '.' or '-')
                || value.Count(character => character == '.') >= 2);
    }

    private static string GetRelativeLocation(string root, string file)
    {
        try
        {
            var relative = Path.GetRelativePath(root, file);
            return Path.GetDirectoryName(relative) is { Length: > 0 } directory
                ? directory
                : "Start menu";
        }
        catch
        {
            return Path.GetDirectoryName(file) ?? string.Empty;
        }
    }

    private sealed record ApplicationEntry(
        string Name,
        string LaunchPath,
        string IconPath,
        string FolderPath,
        string Location,
        double Priority)
    {
        public string SearchText => $"{Name} {Location}";
    }
}

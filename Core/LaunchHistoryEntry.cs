namespace tool_r1ng.Core;

public sealed record LaunchHistoryEntry(
    string Name,
    string LaunchPath,
    string IconPath,
    string FolderPath,
    string Location,
    string Kind = LaunchHistoryKinds.Application,
    bool CloseCommandAfterExecute = false,
    int UseCount = 0,
    DateTime LastUsedUtc = default)
{
    public string SearchText => $"{Name} {Location} {LaunchPath}";
}

public static class LaunchHistoryKinds
{
    public const string Application = "app";
    public const string Command = "command";
    public const string EverythingSearch = "everything-search";
    public const string Path = "path";
}

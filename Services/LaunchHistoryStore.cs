using System.IO;
using System.Text.Json;
using tool_r1ng.Core;

namespace tool_r1ng.Services;

public static class LaunchHistoryStore
{
    private const int MaxEntries = 60;
    private static readonly object SyncRoot = new();
    private static readonly string HistoryDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "tool_r1ng");
    private static readonly string HistoryPath = Path.Combine(HistoryDirectory, "launch-history.json");

    public static IReadOnlyList<LaunchHistoryEntry> Load()
    {
        lock (SyncRoot)
        {
            return LoadEntriesUnsafe();
        }
    }

    public static void Record(LaunchHistoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.LaunchPath))
        {
            return;
        }

        lock (SyncRoot)
        {
            entry = NormalizeEntry(entry);
            var entries = LoadEntriesUnsafe().ToList();
            var existingIndex = entries.FindIndex(item =>
                item.LaunchPath.Equals(entry.LaunchPath, StringComparison.OrdinalIgnoreCase));
            var useCount = 1;
            if (existingIndex >= 0)
            {
                useCount = entries[existingIndex].UseCount + 1;
                entries.RemoveAt(existingIndex);
            }

            entries.Insert(0, entry with
            {
                UseCount = useCount,
                LastUsedUtc = DateTime.UtcNow
            });

            SaveEntriesUnsafe(entries.Take(MaxEntries).ToArray());
        }
    }

    private static IReadOnlyList<LaunchHistoryEntry> LoadEntriesUnsafe()
    {
        try
        {
            if (!File.Exists(HistoryPath))
            {
                return [];
            }

            var entries = JsonSerializer.Deserialize<List<LaunchHistoryEntry>>(
                File.ReadAllText(HistoryPath)) ?? [];
            return entries
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Name)
                    && !string.IsNullOrWhiteSpace(entry.LaunchPath))
                .Select(NormalizeEntry)
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static void SaveEntriesUnsafe(IReadOnlyList<LaunchHistoryEntry> entries)
    {
        try
        {
            Directory.CreateDirectory(HistoryDirectory);
            File.WriteAllText(
                HistoryPath,
                JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
        }
    }

    private static LaunchHistoryEntry NormalizeEntry(LaunchHistoryEntry entry)
    {
        return string.IsNullOrWhiteSpace(entry.Kind)
            ? entry with { Kind = LaunchHistoryKinds.Application }
            : entry;
    }
}

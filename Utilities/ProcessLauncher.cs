using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace tool_r1ng.Utilities;

public static class ProcessLauncher
{
    public static Task OpenAsync(string target)
    {
        if (target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = target,
                UseShellExecute = true
            });

            return Task.CompletedTask;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }

    public static Task OpenContainingFolderAsync(string target)
    {
        var resolvedTarget = ResolveFolderTarget(Environment.ExpandEnvironmentVariables(target));
        var folder = Directory.Exists(resolvedTarget)
            ? resolvedTarget
            : Path.GetDirectoryName(resolvedTarget);

        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return Task.CompletedTask;
        }

        var arguments = File.Exists(resolvedTarget)
            ? $"/select,\"{resolvedTarget}\""
            : $"\"{folder}\"";

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = arguments,
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }

    public static Task OpenEverythingSearchAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.CompletedTask;
        }

        var everythingPath = FindEverythingExecutable();
        var startInfo = new ProcessStartInfo
        {
            FileName = everythingPath ?? "Everything.exe",
            Arguments = $"-s \"{EscapeArgument(query)}\"",
            UseShellExecute = true
        };

        Process.Start(startInfo);
        return Task.CompletedTask;
    }

    private static string? FindEverythingExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Everything", "Everything.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Everything", "Everything.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Everything", "Everything.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Everything", "Everything.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string EscapeArgument(string value)
    {
        return value.Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string ResolveFolderTarget(string target)
    {
        if (!Path.GetExtension(target).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            return target;
        }

        var shortcutTarget = TryResolveShortcutTarget(target);
        return string.IsNullOrWhiteSpace(shortcutTarget)
            ? target
            : shortcutTarget;
    }

    private static string? TryResolveShortcutTarget(string shortcutPath)
    {
        object? shell = null;
        object? shortcut = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                return null;
            }

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return null;
            }

            shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutPath]);

            return shortcut?.GetType().InvokeMember(
                "TargetPath",
                BindingFlags.GetProperty,
                null,
                shortcut,
                null) as string;
        }
        catch
        {
            return null;
        }
        finally
        {
            ReleaseComObject(shortcut);
            ReleaseComObject(shell);
        }
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}

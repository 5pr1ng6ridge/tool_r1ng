using System.Diagnostics;

namespace tool_r1ng.Utilities;

public static class ProcessLauncher
{
    public static Task OpenAsync(string target)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            UseShellExecute = true
        });

        return Task.CompletedTask;
    }
}

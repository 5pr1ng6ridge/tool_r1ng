using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using tool_r1ng.Core;
using tool_r1ng.Utilities;

namespace tool_r1ng.Providers;

public sealed class WindowTitleProvider : tool_r1ng.Core.IQueryProvider
{
    private const int MaxResults = 8;
    private const int MinScore = 28;
    private const int SwRestore = 9;
    private const int GwlStyle = -16;
    private const int SwpNoMove = 0x0002;
    private const int SwpNoSize = 0x0001;
    private const int SwpNoZOrder = 0x0004;
    private const int SwpFrameChanged = 0x0020;
    private const uint MfByCommand = 0x00000000;
    private const uint MfEnabled = 0x00000000;
    private const uint MfGrayed = 0x00000001;
    private const uint ScSize = 0xF000;
    private const uint ScMove = 0xF010;
    private const uint WmClose = 0x0010;
    private const nint WsThickFrame = 0x00040000;
    private const nint WsMaximizeBox = 0x00010000;

    private static readonly Dictionary<nint, LockedWindowState> LockedWindows = [];

    public string Id => "windows";

    public string Name => "Window";

    public ValueTask<IReadOnlyList<QueryResult>> QueryAsync(QueryContext context, CancellationToken cancellationToken)
    {
        if (context.IsEmpty)
        {
            return ValueTask.FromResult<IReadOnlyList<QueryResult>>(Array.Empty<QueryResult>());
        }

        var results = EnumerateWindows()
            .Select(window => new
            {
                Window = window,
                Match = FuzzyMatcher.Match(window.Title, context.Query)
            })
            .Where(item => item.Match.Score >= MinScore)
            .OrderByDescending(item => item.Match.Score)
            .ThenBy(item => item.Window.Title)
            .Take(MaxResults)
            .Select(item => CreateResult(item.Window, item.Match))
            .ToList();

        return ValueTask.FromResult<IReadOnlyList<QueryResult>>(results);
    }

    private QueryResult CreateResult(WindowEntry window, FuzzyMatchResult match)
    {
        var isLocked = IsWindowLocked(window.Handle);
        var subtitle = string.IsNullOrWhiteSpace(window.ProcessName)
            ? "Open window"
            : $"Open window · {window.ProcessName}";

        return new QueryResult
        {
            Title = window.Title,
            HighlightedTitle = HighlightBuilder.Build(window.Title, match.MatchedIndices),
            Subtitle = isLocked ? $"{subtitle} · Locked" : subtitle,
            IconGlyph = "\uE7C4",
            ProviderId = Id,
            ProviderName = Name,
            Score = match.Score + 6,
            ExecuteAsync = _ => ActivateWindowAsync(window.Handle),
            InlineActionGlyph = isLocked ? "\uE785" : "\uE72E",
            InlineActionToolTip = isLocked ? "Unlock window movement" : "Lock window movement",
            InlineActionSuccessStatusText = isLocked ? "Window unlocked" : "Window locked",
            InlineActionAsync = _ => ToggleWindowLockAsync(window.Handle),
            SecondaryActionGlyph = "\uE711",
            SecondaryActionToolTip = "Close window",
            SecondaryActionAsync = _ => CloseWindowAsync(window.Handle)
        };
    }

    private static IEnumerable<WindowEntry> EnumerateWindows()
    {
        var currentProcessId = Environment.ProcessId;
        var windows = new List<WindowEntry>();

        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || handle == IntPtr.Zero)
            {
                return true;
            }

            GetWindowThreadProcessId(handle, out var processId);
            if (processId == currentProcessId)
            {
                return true;
            }

            var titleLength = GetWindowTextLength(handle);
            if (titleLength <= 0)
            {
                return true;
            }

            var titleBuilder = new StringBuilder(titleLength + 1);
            _ = GetWindowText(handle, titleBuilder, titleBuilder.Capacity);
            var title = titleBuilder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            windows.Add(new WindowEntry(handle, title, GetProcessName(processId)));
            return true;
        }, IntPtr.Zero);

        return windows;
    }

    private static Task ActivateWindowAsync(IntPtr handle)
    {
        if (IsIconic(handle))
        {
            _ = ShowWindowAsync(handle, SwRestore);
        }

        _ = SetForegroundWindow(handle);
        return Task.CompletedTask;
    }

    private static Task CloseWindowAsync(IntPtr handle)
    {
        _ = PostMessage(handle, WmClose, IntPtr.Zero, IntPtr.Zero);
        return Task.CompletedTask;
    }

    private static Task ToggleWindowLockAsync(IntPtr handle)
    {
        if (IsWindowLocked(handle))
        {
            UnlockWindow(handle);
        }
        else
        {
            LockWindow(handle);
        }

        return Task.CompletedTask;
    }

    private static void LockWindow(IntPtr handle)
    {
        if (LockedWindows.ContainsKey(handle))
        {
            return;
        }

        var originalStyle = GetWindowLongPtr(handle, GwlStyle);
        LockedWindows[handle] = new LockedWindowState(originalStyle);

        var lockedStyle = originalStyle & ~WsThickFrame & ~WsMaximizeBox;
        _ = SetWindowLongPtr(handle, GwlStyle, lockedStyle);
        SetSystemMenuCommandState(handle, ScSize, isEnabled: false);
        SetSystemMenuCommandState(handle, ScMove, isEnabled: false);
        RefreshWindowFrame(handle);
    }

    private static void UnlockWindow(IntPtr handle)
    {
        if (!LockedWindows.Remove(handle, out var state))
        {
            return;
        }

        _ = SetWindowLongPtr(handle, GwlStyle, state.Style);
        SetSystemMenuCommandState(handle, ScSize, isEnabled: true);
        SetSystemMenuCommandState(handle, ScMove, isEnabled: true);
        RefreshWindowFrame(handle);
    }

    private static bool IsWindowLocked(IntPtr handle)
    {
        return LockedWindows.ContainsKey(handle);
    }

    private static void SetSystemMenuCommandState(IntPtr handle, uint command, bool isEnabled)
    {
        var menu = GetSystemMenu(handle, false);
        if (menu == IntPtr.Zero)
        {
            return;
        }

        var flags = MfByCommand | (isEnabled ? MfEnabled : MfGrayed);
        _ = EnableMenuItem(menu, command, flags);
    }

    private static void RefreshWindowFrame(IntPtr handle)
    {
        _ = SetWindowPos(
            handle,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
    }

    private static string GetProcessName(uint processId)
    {
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch
        {
            return string.Empty;
        }
    }

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(IntPtr handle);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr handle, StringBuilder text, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern bool ShowWindowAsync(IntPtr handle, int commandShow);

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr handle);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr(IntPtr handle, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(IntPtr handle, int index, nint value);

    [DllImport("user32.dll")]
    private static extern IntPtr GetSystemMenu(IntPtr handle, bool revert);

    [DllImport("user32.dll")]
    private static extern uint EnableMenuItem(IntPtr menu, uint itemId, uint enable);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr handle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        int flags);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

    private sealed record LockedWindowState(nint Style);

    private sealed record WindowEntry(IntPtr Handle, string Title, string ProcessName);
}

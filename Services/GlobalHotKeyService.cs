using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace tool_r1ng.Services;

public sealed class GlobalHotKeyService : IDisposable
{
    private const int WmHotKey = 0x0312;
    private const int HotKeyId = 0x5217;

    private HwndSource? _source;
    private nint _windowHandle;
    private bool _registered;

    public event EventHandler? Pressed;

    public string DisplayName { get; private set; } = string.Empty;

    public bool Register(Window window, Key key, ModifierKeys modifiers, string displayName)
    {
        Unregister();

        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.Handle == nint.Zero
            ? helper.EnsureHandle()
            : helper.Handle;

        _source = HwndSource.FromHwnd(_windowHandle);
        _source?.AddHook(WndProc);

        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
        var modifierFlags = ToModifierFlags(modifiers);
        _registered = RegisterHotKey(_windowHandle, HotKeyId, modifierFlags, virtualKey);

        if (_registered)
        {
            DisplayName = displayName;
        }

        return _registered;
    }

    public void Dispose()
    {
        Unregister();
    }

    private void Unregister()
    {
        if (_registered)
        {
            UnregisterHotKey(_windowHandle, HotKeyId);
            _registered = false;
        }

        if (_source is not null)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
    }

    private nint WndProc(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotKey && wParam.ToInt32() == HotKeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            handled = true;
        }

        return nint.Zero;
    }

    private static uint ToModifierFlags(ModifierKeys modifiers)
    {
        uint flags = 0;

        if (modifiers.HasFlag(ModifierKeys.Alt))
        {
            flags |= 0x0001;
        }

        if (modifiers.HasFlag(ModifierKeys.Control))
        {
            flags |= 0x0002;
        }

        if (modifiers.HasFlag(ModifierKeys.Shift))
        {
            flags |= 0x0004;
        }

        if (modifiers.HasFlag(ModifierKeys.Windows))
        {
            flags |= 0x0008;
        }

        return flags;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}

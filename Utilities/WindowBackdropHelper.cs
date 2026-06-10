using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace tool_r1ng.Utilities;

public static class WindowBackdropHelper
{
    private const int WcaAccentPolicy = 19;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;
    private const int DwmWindowCornerPreferenceRound = 2;
    private const int DwmSystemBackdropNone = 1;
    private const int DwmSystemBackdropTransientWindow = 3;
    private const uint RdwInvalidate = 0x0001;
    private const uint RdwInternalPaint = 0x0002;
    private const uint RdwAllChildren = 0x0080;
    private const uint RdwUpdateNow = 0x0100;
    private const uint RdwFrame = 0x0400;
    private const uint RdwErase = 0x0004;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoOwnerZOrder = 0x0200;

    public static void ApplyAcrylic(Window window, bool isEnabled, double opacity)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        PrepareTransparentClient(handle);
        TryApplyRoundedCorners(handle);

        if (!isEnabled)
        {
            TryApplyAccentAcrylic(handle, false, opacity);
            TryApplySystemBackdrop(handle, false);
            TryRefreshComposition(handle);
            return;
        }

        TryApplyAccentAcrylic(handle, false, opacity);
        if (!TryApplySystemBackdrop(handle, true))
        {
            TryApplyAccentAcrylic(handle, true, opacity);
        }

        TryRefreshComposition(handle);
    }

    public static void RefreshComposition(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        TryRefreshComposition(handle);
    }

    public static void RefreshBackdropWithoutResize(Window window, bool isEnabled, double opacity)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == nint.Zero)
        {
            return;
        }

        TryRefreshBackdropWithoutResize(handle, isEnabled, opacity);
    }

    private static void PrepareTransparentClient(nint handle)
    {
        try
        {
            var source = HwndSource.FromHwnd(handle);
            if (source?.CompositionTarget is not null)
            {
                source.CompositionTarget.BackgroundColor = Colors.Transparent;
            }

            var margins = new Margins
            {
                Left = -1,
                Right = -1,
                Top = -1,
                Bottom = -1
            };
            _ = DwmExtendFrameIntoClientArea(handle, ref margins);
        }
        catch
        {
        }
    }

    private static void TryApplyAccentAcrylic(nint handle, bool isEnabled, double opacity)
    {
        var tintOpacity = Math.Clamp(opacity * 0.45, 0.08, 0.58);
        var alpha = (byte)Math.Clamp(tintOpacity * 255, 0, 255);
        var accent = new AccentPolicy
        {
            AccentState = isEnabled
                ? AccentState.AccentEnableAcrylicBlurBehind
                : AccentState.AccentDisabled,
            AccentFlags = 2,
            GradientColor = ToAbgr(alpha, 0xF7, 0xFB, 0xFC)
        };

        var accentSize = Marshal.SizeOf<AccentPolicy>();
        var accentPointer = Marshal.AllocHGlobal(accentSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPointer, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                Data = accentPointer,
                SizeOfData = accentSize
            };

            _ = SetWindowCompositionAttribute(handle, ref data);
        }
        catch
        {
        }
        finally
        {
            Marshal.FreeHGlobal(accentPointer);
        }
    }

    private static bool TryApplySystemBackdrop(nint handle, bool isEnabled)
    {
        try
        {
            var backdropType = isEnabled
                ? DwmSystemBackdropTransientWindow
                : DwmSystemBackdropNone;
            return DwmSetWindowAttribute(
                handle,
                DwmwaSystemBackdropType,
                ref backdropType,
                sizeof(int)) >= 0;
        }
        catch
        {
            return false;
        }
    }

    private static void TryRefreshComposition(nint handle)
    {
        try
        {
            _ = RedrawWindow(
                handle,
                nint.Zero,
                nint.Zero,
                RdwInvalidate | RdwInternalPaint | RdwErase | RdwAllChildren | RdwUpdateNow | RdwFrame);
            _ = DwmFlush();
        }
        catch
        {
        }
    }

    private static void TryRefreshBackdropWithoutResize(nint handle, bool isEnabled, double opacity)
    {
        try
        {
            PrepareTransparentClient(handle);
            TryApplyAccentAcrylic(handle, false, opacity);
            TryApplySystemBackdrop(handle, false);
            TryRefreshFrameWithoutResize(handle);
            _ = DwmFlush();

            PrepareTransparentClient(handle);
            if (isEnabled)
            {
                TryApplyAccentAcrylic(handle, false, opacity);
                if (!TryApplySystemBackdrop(handle, true))
                {
                    TryApplyAccentAcrylic(handle, true, opacity);
                }
            }
            else
            {
                TryApplyAccentAcrylic(handle, false, opacity);
                TryApplySystemBackdrop(handle, false);
            }

            TryRefreshFrameWithoutResize(handle);
            TryRefreshComposition(handle);
        }
        catch
        {
        }
    }

    private static void TryRefreshFrameWithoutResize(nint handle)
    {
        const uint flags = SwpNoSize | SwpNoMove | SwpNoZOrder | SwpNoActivate | SwpFrameChanged | SwpNoOwnerZOrder;
        _ = SetWindowPos(handle, nint.Zero, 0, 0, 0, 0, flags);
    }

    private static void TryApplyRoundedCorners(nint handle)
    {
        try
        {
            var preference = DwmWindowCornerPreferenceRound;
            _ = DwmSetWindowAttribute(
                handle,
                DwmwaWindowCornerPreference,
                ref preference,
                sizeof(int));
        }
        catch
        {
        }
    }

    private static uint ToAbgr(byte alpha, byte red, byte green, byte blue)
    {
        return ((uint)alpha << 24) | ((uint)blue << 16) | ((uint)green << 8) | red;
    }

    private enum AccentState
    {
        AccentDisabled = 0,
        AccentEnableGradient = 1,
        AccentEnableTransparentGradient = 2,
        AccentEnableBlurBehind = 3,
        AccentEnableAcrylicBlurBehind = 4
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public int AccentFlags;
        public uint GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public nint Data;
        public int SizeOfData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(
        nint windowHandle,
        ref WindowCompositionAttributeData data);

    [DllImport("user32.dll")]
    private static extern bool RedrawWindow(
        nint windowHandle,
        nint updateRect,
        nint updateRegion,
        uint flags);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        nint windowHandle,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(
        nint windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(
        nint windowHandle,
        ref Margins margins);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}

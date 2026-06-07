using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace tool_r1ng.Utilities;

public static class IconLoader
{
    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? LoadAssociatedIcon(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var expandedPath = Environment.ExpandEnvironmentVariables(path);
        return Cache.GetOrAdd(expandedPath, LoadIcon);
    }

    private static ImageSource? LoadIcon(string path)
    {
        try
        {
            if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                return LoadShellIcon(path);
            }

            if (!File.Exists(path))
            {
                return null;
            }

            var extension = Path.GetExtension(path);
            if (extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = new MemoryStream(File.ReadAllBytes(path));
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;
                bitmap.DecodePixelWidth = 32;
                bitmap.EndInit();
                bitmap.Freeze();
                return bitmap;
            }

            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon is null)
            {
                return null;
            }

            var image = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? LoadShellIcon(string shellPath)
    {
        var iid = typeof(IShellItemImageFactory).GUID;
        SHCreateItemFromParsingName(shellPath, nint.Zero, ref iid, out var imageFactory);

        imageFactory.GetImage(new NativeSize(48, 48), ShellItemImageOptions.IconOnly, out var bitmapHandle);
        if (bitmapHandle == nint.Zero)
        {
            return null;
        }

        try
        {
            var image = Imaging.CreateBitmapSourceFromHBitmap(
                bitmapHandle,
                nint.Zero,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(32, 32));
            image.Freeze();
            return image;
        }
        finally
        {
            DeleteObject(bitmapHandle);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(
        string path,
        nint bindContext,
        ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory shellItem);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint handle);

    [ComImport]
    [Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory
    {
        void GetImage(NativeSize size, ShellItemImageOptions flags, out nint bitmapHandle);
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativeSize
    {
        public NativeSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }
    }

    [Flags]
    private enum ShellItemImageOptions : uint
    {
        IconOnly = 0x00000004
    }
}

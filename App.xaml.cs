using System.Drawing;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;

namespace tool_r1ng;

public partial class App : System.Windows.Application
{
    private Icon? _appIcon;
    private Forms.NotifyIcon? _trayIcon;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _mainWindow = new MainWindow();
        CreateTrayIcon();
        _mainWindow.ShowLauncher();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mainWindow?.PrepareForShutdown();
        _trayIcon?.Dispose();
        _appIcon?.Dispose();
        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        _appIcon = LoadApplicationIcon();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(CreateMenuItem("Show launcher", (_, _) => _mainWindow?.ShowLauncher()));
        menu.Items.Add(CreateMenuItem("Exit", (_, _) =>
        {
            _mainWindow?.PrepareForShutdown();
            Shutdown();
        }));

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = _appIcon,
            Text = "tool_r1ng",
            ContextMenuStrip = menu,
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => _mainWindow?.ShowLauncher();
    }

    private Forms.ToolStripMenuItem CreateMenuItem(string text, EventHandler onClick)
    {
        var item = new Forms.ToolStripMenuItem(text);
        item.Click += onClick;
        if (_appIcon is not null)
        {
            item.Image = _appIcon.ToBitmap();
        }

        return item;
    }

    private static Icon LoadApplicationIcon()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "app.ico");
            if (File.Exists(iconPath))
            {
                return new Icon(iconPath);
            }

            if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            {
                return Icon.ExtractAssociatedIcon(Environment.ProcessPath) ?? SystemIcons.Application;
            }
        }
        catch
        {
        }

        return SystemIcons.Application;
    }
}

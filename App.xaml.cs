using System.Windows;
using Forms = System.Windows.Forms;

namespace tool_r1ng;

public partial class App : System.Windows.Application
{
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
        base.OnExit(e);
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show launcher", null, (_, _) => _mainWindow?.ShowLauncher());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _mainWindow?.PrepareForShutdown();
            Shutdown();
        });

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "tool_r1ng",
            ContextMenuStrip = menu,
            Visible = true
        };

        _trayIcon.DoubleClick += (_, _) => _mainWindow?.ShowLauncher();
    }
}

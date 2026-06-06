using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using tool_r1ng.Providers;
using tool_r1ng.Services;
using tool_r1ng.ViewModels;

namespace tool_r1ng;

public partial class MainWindow : Window
{
    private readonly LauncherViewModel _viewModel;
    private readonly GlobalHotKeyService _hotKeyService = new();
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();

        var engine = new LauncherEngine([
            new CalculatorProvider(),
            new ApplicationsProvider(),
            new WebSearchProvider()
        ]);

        _viewModel = new LauncherViewModel(engine);
        _viewModel.RequestHide += (_, _) => HideLauncher();
        DataContext = _viewModel;

        SourceInitialized += (_, _) => RegisterHotKey();
        Loaded += async (_, _) =>
        {
            await _viewModel.RefreshResultsAsync();
            QueryBox.Focus();
        };
    }

    public void ShowLauncher()
    {
        CenterLauncher();
        Show();
        Activate();
        QueryBox.Focus();
        QueryBox.SelectAll();
    }

    public void PrepareForShutdown()
    {
        _allowClose = true;
        _hotKeyService.Dispose();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideLauncher();
            return;
        }

        base.OnClosing(e);
    }

    private void RegisterHotKey()
    {
        _hotKeyService.Pressed += (_, _) => ToggleLauncher();

        if (_hotKeyService.Register(this, Key.Space, ModifierKeys.Alt, "Alt + Space"))
        {
            return;
        }

        if (!_hotKeyService.Register(this, Key.Space, ModifierKeys.Control, "Ctrl + Space"))
        {
            _viewModel.StatusText = "Global hotkey unavailable";
        }
    }

    private void ToggleLauncher()
    {
        if (IsVisible && IsActive)
        {
            HideLauncher();
            return;
        }

        ShowLauncher();
    }

    private void HideLauncher()
    {
        Hide();
    }

    private void CenterLauncher()
    {
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + workArea.Height * 0.18;
    }

    private void Window_Deactivated(object sender, EventArgs e)
    {
        HideLauncher();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                HideLauncher();
                e.Handled = true;
                break;
            case Key.Enter:
                _viewModel.ExecuteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Down:
                _viewModel.MoveSelection(1);
                ResultsList.ScrollIntoView(_viewModel.SelectedResult);
                e.Handled = true;
                break;
            case Key.Up:
                _viewModel.MoveSelection(-1);
                ResultsList.ScrollIntoView(_viewModel.SelectedResult);
                e.Handled = true;
                break;
        }
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _viewModel.ExecuteSelectedCommand.Execute(null);
    }
}

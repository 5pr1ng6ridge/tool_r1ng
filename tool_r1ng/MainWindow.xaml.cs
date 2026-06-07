using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using tool_r1ng.Core;
using tool_r1ng.Providers;
using tool_r1ng.Services;
using tool_r1ng.ViewModels;

namespace tool_r1ng;

public partial class MainWindow : Window
{
    private const double LauncherMaxHeight = 500;
    private const double HeaderHeight = 52;
    private const double FooterHeight = 12;
    private const double ResultsListVerticalSpacing = 14;
    private const double ResultRowHeight = 64;

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
        _viewModel.ResultsUpdated += (_, _) => UpdateLauncherHeight();
        DataContext = _viewModel;

        SourceInitialized += (_, _) => RegisterHotKey();
        Loaded += async (_, _) =>
        {
            await _viewModel.RefreshResultsAsync();
            UpdateLauncherHeight();
            QueryBox.Focus();
        };
    }

    public void ShowLauncher()
    {
        CenterLauncher();
        UpdateLauncherHeight();
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
        MaxHeight = Math.Min(LauncherMaxHeight, Math.Max(MinHeight, workArea.Bottom - Top - 8));
    }

    private void UpdateLauncherHeight()
    {
        var desiredHeight = _viewModel.Results.Count == 0
            ? HeaderHeight
            : HeaderHeight
              + FooterHeight
              + ResultsListVerticalSpacing
              + _viewModel.Results.Count * ResultRowHeight;

        Height = Math.Min(MaxHeight, Math.Max(MinHeight, desiredHeight));
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

    private async void SecondaryActionButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not FrameworkElement { DataContext: QueryResult result }
            || result.SecondaryActionAsync is null)
        {
            return;
        }

        try
        {
            await result.SecondaryActionAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _viewModel.StatusText = exception.Message;
        }
    }
}

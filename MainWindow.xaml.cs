using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using tool_r1ng.Core;
using tool_r1ng.Providers;
using tool_r1ng.Services;
using tool_r1ng.Utilities;
using tool_r1ng.ViewModels;

namespace tool_r1ng;

public partial class MainWindow : Window
{
    private const int WmActivate = 0x0006;
    private const int WmShowWindow = 0x0018;
    private const int WmWindowPosChanged = 0x0047;
    private const int WmSettingChange = 0x001A;
    private const int WmThemeChanged = 0x031A;
    private const int WmDwmCompositionChanged = 0x031E;
    private const double LauncherMaxHeight = 500;
    private const double HeaderHeight = 52;
    private const double FooterHeight = 12;
    private const double ResultsListVerticalSpacing = 14;
    private const double ResultRowHeight = 64;
    private const double SettingsPanelHeight = 278;

    private readonly LauncherViewModel _viewModel;
    private readonly GlobalHotKeyService _hotKeyService = new();
    private HwndSource? _windowSource;
    private bool _allowClose;
    private int _appearanceRefreshVersion;

    public MainWindow()
    {
        InitializeComponent();

        var settings = LauncherSettings.Load();
        var engine = new LauncherEngine([
            new CalculatorProvider(),
            new ApplicationsProvider(settings),
            new EverythingProvider(settings),
            new WindowTitleProvider(),
            new WebSearchProvider()
        ]);
        _ = engine.WarmUpAsync(CancellationToken.None);

        _viewModel = new LauncherViewModel(engine, settings);
        _viewModel.RequestHide += (_, _) => HideLauncher();
        _viewModel.ResultsUpdated += (_, _) =>
        {
            UpdateLauncherHeight();
        };
        _viewModel.AppearanceSettingsChanged += (_, _) => RefreshAppearance();
        DataContext = _viewModel;

        SourceInitialized += (_, _) =>
        {
            _windowSource = (HwndSource?)PresentationSource.FromVisual(this);
            _windowSource?.AddHook(WindowMessageHook);
            RefreshAppearance();
            RegisterHotKey();
        };
        Loaded += async (_, _) =>
        {
            await _viewModel.RefreshResultsAsync();
            UpdateLauncherHeight();
            RefreshAppearance();
            QueryBox.Focus();
        };

        TextCompositionManager.AddPreviewTextInputStartHandler(QueryBox, QueryBox_TextInputStart);
        TextCompositionManager.AddPreviewTextInputUpdateHandler(QueryBox, QueryBox_TextInputUpdate);
        TextCompositionManager.AddPreviewTextInputHandler(QueryBox, QueryBox_TextInputCommitted);
    }

    public void ShowLauncher()
    {
        CenterLauncher();
        UpdateLauncherHeight();
        Show();
        Activate();
        UpdateLayout();
        QueryBox.Focus();
        QueryBox.SelectAll();
        RefreshAppearance();
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

    protected override void OnClosed(EventArgs e)
    {
        _windowSource?.RemoveHook(WindowMessageHook);
        _windowSource = null;
        base.OnClosed(e);
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

    private nint WindowMessageHook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        switch (message)
        {
            case WmShowWindow when wParam != nint.Zero:
            case WmActivate when wParam != nint.Zero:
            case WmWindowPosChanged when IsVisible:
            case WmDwmCompositionChanged:
            case WmThemeChanged:
            case WmSettingChange:
                RefreshAppearance();
                break;
        }

        return nint.Zero;
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
        var desiredHeight = _viewModel.IsSettingsVisible
            ? HeaderHeight + SettingsPanelHeight + FooterHeight
            : _viewModel.Results.Count == 0
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
            case Key.Tab:
                _viewModel.CompleteWithSelectedResult();
                Dispatcher.BeginInvoke(() =>
                {
                    QueryBox.CaretIndex = QueryBox.Text.Length;
                    QueryBox.Focus();
                });
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

    private void QueryBox_TextInputStart(object sender, TextCompositionEventArgs e)
    {
        _viewModel.SetCompletionSuppressed(true);
    }

    private void QueryBox_TextInputUpdate(object sender, TextCompositionEventArgs e)
    {
        _viewModel.SetCompletionSuppressed(true);
    }

    private void QueryBox_TextInputCommitted(object sender, TextCompositionEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _viewModel.SetCompletionSuppressed(false);
        });
    }

    private void ApplyAppearance()
    {
        WindowBackdropHelper.ApplyAcrylic(
            this,
            _viewModel.AcrylicBackdropEnabled,
            _viewModel.AcrylicOpacity);

        var opacity = _viewModel.AcrylicBackdropEnabled
            ? _viewModel.AcrylicOpacity
            : 0.96;

        var rootOpacity = _viewModel.AcrylicBackdropEnabled
            ? Math.Clamp(opacity * 0.56, 0.16, 0.62)
            : opacity;
        var headerOpacity = _viewModel.AcrylicBackdropEnabled
            ? Math.Clamp(opacity * 0.44, 0.14, 0.54)
            : 0.92;
        var settingsOpacity = _viewModel.AcrylicBackdropEnabled
            ? Math.Clamp(opacity * 0.72, 0.22, 0.78)
            : 0.96;
        var borderOpacity = _viewModel.AcrylicBackdropEnabled
            ? Math.Clamp(opacity * 0.62, 0.18, 0.60)
            : 0.62;

        RootMaterial.Background = CreateWhiteBrush(rootOpacity);
        HeaderMaterial.Background = CreateWhiteBrush(headerOpacity);
        SettingsMaterial.Background = CreateWhiteBrush(settingsOpacity);
        RootMaterial.BorderBrush = CreateBrush(borderOpacity, 214, 208, 195);
        HeaderMaterial.BorderBrush = CreateBrush(borderOpacity * 0.82, 228, 222, 207);
        SettingsMaterial.BorderBrush = CreateBrush(borderOpacity * 0.88, 228, 222, 207);
    }

    private void RefreshAppearance()
    {
        ApplyAppearance();
        InvalidateAppearance();
        ScheduleDeferredAppearanceRefresh();
    }

    private void ScheduleDeferredAppearanceRefresh()
    {
        var refreshVersion = ++_appearanceRefreshVersion;

        Dispatcher.BeginInvoke(
            (Action)(() =>
            {
                ApplyDeferredAppearanceRefresh(refreshVersion);
            }),
            DispatcherPriority.Render);

        Dispatcher.BeginInvoke(
            (Action)(() =>
            {
                ApplyDeferredAppearanceRefresh(refreshVersion);
            }),
            DispatcherPriority.ApplicationIdle);

        _ = ApplyDelayedAppearanceRefreshAsync(refreshVersion);
    }

    private async Task ApplyDelayedAppearanceRefreshAsync(int refreshVersion)
    {
        foreach (var delay in new[] { 32, 96, 180 })
        {
            await Task.Delay(delay);

            if (refreshVersion != _appearanceRefreshVersion)
            {
                return;
            }

            await Dispatcher.InvokeAsync(
                () => ApplyDeferredAppearanceRefresh(refreshVersion),
                DispatcherPriority.Render);
        }
    }

    private void ApplyDeferredAppearanceRefresh(int refreshVersion)
    {
        if (refreshVersion != _appearanceRefreshVersion || !IsVisible)
        {
            return;
        }

        ApplyAppearance();
        InvalidateAppearance();
    }

    private void InvalidateAppearance()
    {
        RootMaterial.InvalidateVisual();
        HeaderMaterial.InvalidateVisual();
        SettingsMaterial.InvalidateVisual();
        InvalidateVisual();
        WindowBackdropHelper.RefreshComposition(this);
    }

    private static byte ToAlpha(double opacity)
    {
        return (byte)Math.Clamp(opacity * 255, 0, 255);
    }

    private static SolidColorBrush CreateWhiteBrush(double opacity)
    {
        return CreateBrush(opacity, 255, 255, 255);
    }

    private static SolidColorBrush CreateBrush(double opacity, byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(ToAlpha(opacity), red, green, blue));
        brush.Freeze();
        return brush;
    }

    private async void EverythingFileSearchToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Primitives.ToggleButton toggle)
        {
            return;
        }

        await _viewModel.SetEverythingFileSearchEnabledAsync(toggle.IsChecked == true);
    }

    private async void EverythingAppSearchToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Primitives.ToggleButton toggle)
        {
            return;
        }

        await _viewModel.SetEverythingAppSearchEnabledAsync(toggle.IsChecked == true);
    }

    private void AcrylicBackdropToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Primitives.ToggleButton toggle)
        {
            return;
        }

        _viewModel.SetAcrylicBackdropEnabled(toggle.IsChecked == true);
    }

    private void AcrylicOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DataContext is not LauncherViewModel viewModel)
        {
            return;
        }

        viewModel.SetAcrylicOpacity(e.NewValue);
    }

    private async void InlineActionButton_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not FrameworkElement { DataContext: QueryResult result }
            || result.InlineActionAsync is null)
        {
            return;
        }

        try
        {
            await result.InlineActionAsync(CancellationToken.None);
            await _viewModel.RefreshResultsAsync();
            _viewModel.StatusText = result.InlineActionSuccessStatusText;
        }
        catch (Exception exception)
        {
            _viewModel.StatusText = exception.Message;
        }
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

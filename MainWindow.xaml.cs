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
    private const double SettingsPanelHeight = 436;

    private readonly LauncherViewModel _viewModel;
    private readonly GlobalHotKeyService _hotKeyService = new();
    private HwndSource? _windowSource;
    private bool _allowClose;
    private bool _isShowingLauncher;
    private bool? _lastAppliedAcrylicBackdropEnabled;
    private double _lastAppliedAcrylicOpacity = double.NaN;
    private System.Windows.Media.Color? _lastAppliedAcrylicTintColor;
    private int _wakeRefreshVersion;

    public MainWindow()
    {
        InitializeComponent();

        var settings = LauncherSettings.Load();
        var engine = new LauncherEngine([
            new CalculatorProvider(),
            new CommandProvider(),
            new ApplicationHistoryProvider(),
            new PathProvider(),
            new WindowsSettingsProvider(),
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
            RefreshAppearance(forceBackdrop: true);
            RegisterHotKey();
        };
        Loaded += async (_, _) =>
        {
            await _viewModel.RefreshResultsAsync();
            UpdateLauncherHeight();
            RefreshAppearanceForWake();
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
        EnsureNativeHandle();
        ApplyAppearance(forceBackdrop: true);

        try
        {
            _isShowingLauncher = true;
            Show();
            Activate();
        }
        finally
        {
            _isShowingLauncher = false;
        }

        UpdateLayout();
        QueryBox.Focus();
        QueryBox.SelectAll();
        RefreshAppearanceForWake(forceBackdrop: true);
        _ = _viewModel.RefreshResultsAsync();
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
                if (!_isShowingLauncher)
                {
                    RefreshAppearanceForWake(forceBackdrop: true);
                }
                break;
            case WmWindowPosChanged when IsVisible:
                RefreshAppearance();
                break;
            case WmDwmCompositionChanged:
            case WmThemeChanged:
            case WmSettingChange:
                RefreshAppearance(forceBackdrop: true);
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
        if (IsSettingsTextInput(e.OriginalSource))
        {
            return;
        }

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
        RefreshResultsForComposition(e);
    }

    private void QueryBox_TextInputUpdate(object sender, TextCompositionEventArgs e)
    {
        _viewModel.SetCompletionSuppressed(true);
        RefreshResultsForComposition(e);
    }

    private void QueryBox_TextInputCommitted(object sender, TextCompositionEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _viewModel.SetCompletionSuppressed(false);
        });
    }

    private void RefreshResultsForComposition(TextCompositionEventArgs e)
    {
        var compositionText = e.TextComposition.CompositionText;
        if (string.IsNullOrEmpty(compositionText))
        {
            compositionText = e.TextComposition.Text;
        }

        if (string.IsNullOrEmpty(compositionText))
        {
            return;
        }

        _ = _viewModel.RefreshResultsForPreviewTextAsync(BuildPreviewQueryText(compositionText));
    }

    private string BuildPreviewQueryText(string compositionText)
    {
        var text = QueryBox.Text ?? string.Empty;
        var selectionStart = Math.Clamp(QueryBox.SelectionStart, 0, text.Length);
        var selectionLength = Math.Clamp(QueryBox.SelectionLength, 0, text.Length - selectionStart);

        return text.Remove(selectionStart, selectionLength).Insert(selectionStart, compositionText);
    }

    private void EnsureNativeHandle()
    {
        var helper = new WindowInteropHelper(this);
        if (helper.Handle == nint.Zero)
        {
            helper.EnsureHandle();
        }
    }

    private bool IsSettingsTextInput(object source)
    {
        if (source is not DependencyObject dependencyObject)
        {
            return false;
        }

        var textBox = FindVisualParent<System.Windows.Controls.TextBox>(dependencyObject);
        return textBox is not null && !ReferenceEquals(textBox, QueryBox);
    }

    private static T? FindVisualParent<T>(DependencyObject dependencyObject)
        where T : DependencyObject
    {
        var current = dependencyObject;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = GetParent(current);
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject dependencyObject)
    {
        try
        {
            return VisualTreeHelper.GetParent(dependencyObject)
                ?? LogicalTreeHelper.GetParent(dependencyObject);
        }
        catch (InvalidOperationException)
        {
            return LogicalTreeHelper.GetParent(dependencyObject);
        }
    }

    private void ApplyAppearance(bool forceBackdrop = false)
    {
        if (NeedsNativeBackdropUpdate(forceBackdrop)
            && WindowBackdropHelper.ApplyAcrylic(
                this,
                _viewModel.AcrylicBackdropEnabled,
                _viewModel.AcrylicOpacity,
                _viewModel.WindowMaterialColorValue))
        {
            _lastAppliedAcrylicBackdropEnabled = _viewModel.AcrylicBackdropEnabled;
            _lastAppliedAcrylicOpacity = _viewModel.AcrylicOpacity;
            _lastAppliedAcrylicTintColor = _viewModel.WindowMaterialColorValue;
        }

        var opacity = _viewModel.AcrylicBackdropEnabled
            ? _viewModel.AcrylicOpacity
            : 0.96;

        var rootOpacity = _viewModel.AcrylicBackdropEnabled
            ? Math.Clamp(opacity * 0.04, 0.005, 0.42)
            : opacity;
        var headerOpacity = _viewModel.AcrylicBackdropEnabled
            ? Math.Clamp(opacity * 0.28, 0.025, 0.36)
            : 0.92;
        var settingsOpacity = _viewModel.AcrylicBackdropEnabled
            ? Math.Clamp(opacity * 0.42, 0.05, 0.52)
            : 0.96;
        var borderOpacity = _viewModel.AcrylicBackdropEnabled
            ? Math.Clamp(opacity * 0.40, 0.08, 0.44)
            : 0.62;

        RootMaterial.Background = CreateBrush(rootOpacity, _viewModel.WindowMaterialColorValue);
        HeaderMaterial.Background = CreateBrush(headerOpacity, _viewModel.HeaderMaterialColorValue);
        SettingsMaterial.Background = CreateBrush(settingsOpacity, _viewModel.SettingsMaterialColorValue);
        RootMaterial.BorderBrush = CreateBrush(borderOpacity, _viewModel.BorderMaterialColorValue);
        HeaderMaterial.BorderBrush = CreateBrush(borderOpacity * 0.82, _viewModel.BorderMaterialColorValue);
        SettingsMaterial.BorderBrush = CreateBrush(borderOpacity * 0.88, _viewModel.BorderMaterialColorValue);
    }

    private bool NeedsNativeBackdropUpdate(bool forceBackdrop)
    {
        return forceBackdrop
            || _lastAppliedAcrylicBackdropEnabled != _viewModel.AcrylicBackdropEnabled
            || Math.Abs(_lastAppliedAcrylicOpacity - _viewModel.AcrylicOpacity) >= 0.001
            || _lastAppliedAcrylicTintColor != _viewModel.WindowMaterialColorValue;
    }

    private void RefreshAppearance(bool forceBackdrop = false)
    {
        ApplyAppearance(forceBackdrop);
        InvalidateAppearance();
    }

    private void RefreshAppearanceForWake(bool forceBackdrop = false)
    {
        ApplyAppearance(forceBackdrop);
        InvalidateAppearance();
        ScheduleWakeCompositionRefresh();
    }

    private void ScheduleWakeCompositionRefresh()
    {
        var refreshVersion = ++_wakeRefreshVersion;

        Dispatcher.BeginInvoke(
            (Action)(() =>
            {
                ApplyWakeCompositionRefresh(refreshVersion);
            }),
            DispatcherPriority.Render);

        _ = ApplyDelayedWakeCompositionRefreshAsync(refreshVersion);
    }

    private async Task ApplyDelayedWakeCompositionRefreshAsync(int refreshVersion)
    {
        await Task.Delay(48);

        if (refreshVersion != _wakeRefreshVersion)
        {
            return;
        }

        await Dispatcher.InvokeAsync(
            () => ApplyWakeCompositionRefresh(refreshVersion),
            DispatcherPriority.ContextIdle);
    }

    private void ApplyWakeCompositionRefresh(int refreshVersion)
    {
        if (refreshVersion != _wakeRefreshVersion || !IsVisible)
        {
            return;
        }

        ApplyAppearance();
        InvalidateAppearance();
        WindowBackdropHelper.RefreshComposition(this);
    }

    private void InvalidateAppearance()
    {
        RootMaterial.InvalidateVisual();
        HeaderMaterial.InvalidateVisual();
        SettingsMaterial.InvalidateVisual();
        InvalidateVisual();
    }

    private static byte ToAlpha(double opacity)
    {
        return (byte)Math.Clamp(opacity * 255, 0, 255);
    }

    private static SolidColorBrush CreateBrush(double opacity, System.Windows.Media.Color color)
    {
        return CreateBrush(opacity, color.R, color.G, color.B);
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

    private void ColorTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox textBox)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Enter:
                textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateSource();
                Keyboard.ClearFocus();
                e.Handled = true;
                break;
            case Key.Escape:
                textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateTarget();
                Keyboard.ClearFocus();
                e.Handled = true;
                break;
        }
    }

    private void ResetMaterialColors_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.ResetMaterialColors();
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

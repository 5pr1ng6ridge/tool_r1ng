using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using tool_r1ng.Core;
using tool_r1ng.Services;
using tool_r1ng.Utilities;

namespace tool_r1ng.ViewModels;

public sealed class LauncherViewModel : INotifyPropertyChanged
{
    private readonly LauncherEngine _engine;
    private readonly LauncherSettings _settings;
    private readonly AsyncRelayCommand _executeSelectedCommand;
    private CancellationTokenSource? _searchCancellation;
    private string _queryText = string.Empty;
    private QueryResult? _selectedResult;
    private bool _isSearching;
    private string _statusText = string.Empty;
    private string _completionSuffix = string.Empty;
    private string? _completionTitle;
    private bool _isCompletionSuppressed;
    private bool _everythingFileSearchEnabled;
    private bool _everythingAppSearchEnabled;
    private string _everythingStatusText = string.Empty;
    private bool _isRefreshingEverythingStatus;

    public LauncherViewModel(LauncherEngine engine, LauncherSettings settings)
    {
        _engine = engine;
        _settings = settings;
        _everythingFileSearchEnabled = settings.EnableEverythingFileSearch;
        _everythingAppSearchEnabled = settings.EnableEverythingAppSearch;
        EverythingStatusText = "Everything 状态未检测";
        _executeSelectedCommand = new AsyncRelayCommand(_ => ExecuteSelectedAsync(), _ => SelectedResult is not null);
        HideCommand = new AsyncRelayCommand(_ =>
        {
            RequestHide?.Invoke(this, EventArgs.Empty);
            return Task.CompletedTask;
        });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? RequestHide;

    public event EventHandler? ResultsUpdated;

    public ObservableCollection<QueryResult> Results { get; } = [];

    public ICommand ExecuteSelectedCommand => _executeSelectedCommand;

    public ICommand HideCommand { get; }

    public string QueryText
    {
        get => _queryText;
        set
        {
            if (_queryText == value)
            {
                return;
            }

            var previousText = _queryText;
            var previousCompletionTitle = _completionTitle;

            _queryText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CompletionPrefix));
            OnPropertyChanged(nameof(IsSettingsVisible));
            OnPropertyChanged(nameof(IsResultsVisible));
            if (IsSettingsVisible)
            {
                _ = RefreshEverythingStatusAsync();
            }

            if (value.Length < previousText.Length)
            {
                TryUpdateCompletionSuffix(previousCompletionTitle);
            }
            else
            {
                ClearCompletion();
            }

            _ = RefreshResultsAsync();
        }
    }

    public string CompletionPrefix => QueryText;

    public bool IsSettingsVisible => QueryText.TrimStart().StartsWith("/", StringComparison.Ordinal);

    public bool IsResultsVisible => !IsSettingsVisible;

    public string CompletionSuffix
    {
        get => _completionSuffix;
        private set
        {
            if (_completionSuffix == value)
            {
                return;
            }

            _completionSuffix = value;
            OnPropertyChanged();
        }
    }

    public QueryResult? SelectedResult
    {
        get => _selectedResult;
        set
        {
            if (_selectedResult == value)
            {
                return;
            }

            _selectedResult = value;
            OnPropertyChanged();
            UpdateCompletionSuffix();
            _executeSelectedCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set
        {
            if (_isSearching == value)
            {
                return;
            }

            _isSearching = value;
            OnPropertyChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText == value)
            {
                return;
            }

            _statusText = value;
            OnPropertyChanged();
        }
    }

    public bool EverythingFileSearchEnabled
    {
        get => _everythingFileSearchEnabled;
        private set
        {
            if (_everythingFileSearchEnabled == value)
            {
                return;
            }

            _everythingFileSearchEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool EverythingAppSearchEnabled
    {
        get => _everythingAppSearchEnabled;
        private set
        {
            if (_everythingAppSearchEnabled == value)
            {
                return;
            }

            _everythingAppSearchEnabled = value;
            OnPropertyChanged();
        }
    }

    public string EverythingStatusText
    {
        get => _everythingStatusText;
        private set
        {
            if (_everythingStatusText == value)
            {
                return;
            }

            _everythingStatusText = value;
            OnPropertyChanged();
        }
    }

    public async Task RefreshResultsAsync()
    {
        _searchCancellation?.Cancel();
        _searchCancellation = new CancellationTokenSource();
        var cancellationToken = _searchCancellation.Token;

        try
        {
            if (string.IsNullOrWhiteSpace(QueryText))
            {
                ReplaceResults([], cancellationToken);
                return;
            }

            if (IsSettingsVisible)
            {
                ReplaceResults([], cancellationToken);
                return;
            }

            IsSearching = true;
            await Task.Delay(80, cancellationToken);
            var query = QueryText;
            var results = await Task.Run(
                () => _engine.SearchAsync(query, cancellationToken),
                cancellationToken);
            ReplaceResults(results, cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!cancellationToken.IsCancellationRequested)
            {
                IsSearching = false;
            }
        }
    }

    private void ReplaceResults(IReadOnlyList<QueryResult> results, CancellationToken cancellationToken)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            Results.Clear();
            foreach (var result in results)
            {
                Results.Add(result);
            }

            SelectedResult = Results.FirstOrDefault();
            UpdateCompletionSuffix();
            StatusText = string.Empty;
            ResultsUpdated?.Invoke(this, EventArgs.Empty);
        });
    }

    public void MoveSelection(int offset)
    {
        if (Results.Count == 0)
        {
            return;
        }

        var currentIndex = SelectedResult is null ? -1 : Results.IndexOf(SelectedResult);
        var nextIndex = currentIndex + offset;

        if (nextIndex < 0)
        {
            nextIndex = Results.Count - 1;
        }
        else if (nextIndex >= Results.Count)
        {
            nextIndex = 0;
        }

        SelectedResult = Results[nextIndex];
    }

    public void CompleteWithSelectedResult()
    {
        if (SelectedResult is null)
        {
            return;
        }

        QueryText = string.IsNullOrEmpty(CompletionSuffix)
            ? SelectedResult.Title
            : QueryText + CompletionSuffix;
        ClearCompletion();
    }

    public void SetCompletionSuppressed(bool isSuppressed)
    {
        if (_isCompletionSuppressed == isSuppressed)
        {
            return;
        }

        _isCompletionSuppressed = isSuppressed;
        UpdateCompletionSuffix();
    }

    public void Reset()
    {
        QueryText = string.Empty;
        _ = RefreshResultsAsync();
    }

    public async Task SetEverythingFileSearchEnabledAsync(bool isEnabled)
    {
        if (!await TryApplyEverythingSettingAsync(isEnabled))
        {
            EverythingFileSearchEnabled = _settings.EnableEverythingFileSearch;
            return;
        }

        _settings.SetEverythingFileSearch(isEnabled);
        EverythingFileSearchEnabled = isEnabled;
        await RefreshEverythingStatusAsync();
        StatusText = isEnabled ? "Everything 文件搜索已启用" : "Everything 文件搜索已停用";
    }

    public async Task SetEverythingAppSearchEnabledAsync(bool isEnabled)
    {
        if (!await TryApplyEverythingSettingAsync(isEnabled))
        {
            EverythingAppSearchEnabled = _settings.EnableEverythingAppSearch;
            return;
        }

        _settings.SetEverythingAppSearch(isEnabled);
        EverythingAppSearchEnabled = isEnabled;
        await RefreshEverythingStatusAsync();
        await RefreshResultsAsync();
        StatusText = isEnabled ? "Everything 应用搜索已启用" : "Everything 应用搜索已停用";
    }

    private async Task ExecuteSelectedAsync()
    {
        var result = SelectedResult;
        if (result is null)
        {
            return;
        }

        try
        {
            await result.ExecuteAsync(CancellationToken.None);
            if (result.DismissAfterExecute)
            {
                RequestHide?.Invoke(this, EventArgs.Empty);
                Reset();
                return;
            }

            await RefreshResultsAsync();
            StatusText = result.SuccessStatusText;
        }
        catch (Exception exception)
        {
            StatusText = exception.Message;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private async Task<bool> TryApplyEverythingSettingAsync(bool isEnabled)
    {
        if (!isEnabled)
        {
            return true;
        }

        var status = await Task.Run(() =>
        {
            var isAvailable = EverythingClient.TryCheckAvailability(out var message);
            return new EverythingAvailability(isAvailable, message);
        });

        EverythingStatusText = status.Message;
        if (status.IsAvailable)
        {
            return true;
        }

        StatusText = status.Message;
        return false;
    }

    private async Task RefreshEverythingStatusAsync()
    {
        if (_isRefreshingEverythingStatus)
        {
            return;
        }

        _isRefreshingEverythingStatus = true;
        try
        {
            var status = await Task.Run(() =>
            {
                var isAvailable = EverythingClient.TryCheckAvailability(out var message);
                return new EverythingAvailability(isAvailable, message);
            });

            EverythingStatusText = status.IsAvailable ? "Everything 已就绪" : status.Message;
        }
        finally
        {
            _isRefreshingEverythingStatus = false;
        }
    }

    private void UpdateCompletionSuffix()
    {
        if (SelectedResult is null)
        {
            ClearCompletion();
            return;
        }

        TryUpdateCompletionSuffix(SelectedResult.Title);
    }

    private bool TryUpdateCompletionSuffix(string? title)
    {
        if (title is null
            || _isCompletionSuppressed
            || string.IsNullOrWhiteSpace(QueryText)
            || char.IsWhiteSpace(QueryText[^1])
            || title.Length <= QueryText.Length
            || !title.StartsWith(QueryText, StringComparison.CurrentCultureIgnoreCase))
        {
            ClearCompletion();
            return false;
        }

        _completionTitle = title;
        CompletionSuffix = title[QueryText.Length..];
        return true;
    }

    private void ClearCompletion()
    {
        _completionTitle = null;
        CompletionSuffix = string.Empty;
    }

    private sealed record EverythingAvailability(bool IsAvailable, string Message);
}

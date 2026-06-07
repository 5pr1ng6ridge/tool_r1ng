using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using tool_r1ng.Core;
using tool_r1ng.Services;

namespace tool_r1ng.ViewModels;

public sealed class LauncherViewModel : INotifyPropertyChanged
{
    private readonly LauncherEngine _engine;
    private readonly AsyncRelayCommand _executeSelectedCommand;
    private CancellationTokenSource? _searchCancellation;
    private string _queryText = string.Empty;
    private QueryResult? _selectedResult;
    private bool _isSearching;
    private string _statusText = string.Empty;
    private string _completionSuffix = string.Empty;
    private bool _isCompletionSuppressed;

    public LauncherViewModel(LauncherEngine engine)
    {
        _engine = engine;
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

            _queryText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CompletionPrefix));
            UpdateCompletionSuffix();
            _ = RefreshResultsAsync();
        }
    }

    public string CompletionPrefix => QueryText;

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

            IsSearching = true;
            await Task.Delay(80, cancellationToken);
            var results = await _engine.SearchAsync(QueryText, cancellationToken);
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
        CompletionSuffix = string.Empty;
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

    private async Task ExecuteSelectedAsync()
    {
        if (SelectedResult is null)
        {
            return;
        }

        try
        {
            await SelectedResult.ExecuteAsync(CancellationToken.None);
            RequestHide?.Invoke(this, EventArgs.Empty);
            Reset();
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

    private void UpdateCompletionSuffix()
    {
        if (SelectedResult is null
            || _isCompletionSuppressed
            || string.IsNullOrWhiteSpace(QueryText)
            || char.IsWhiteSpace(QueryText[^1])
            || SelectedResult.Title.Length <= QueryText.Length
            || !SelectedResult.Title.StartsWith(QueryText, StringComparison.CurrentCultureIgnoreCase))
        {
            CompletionSuffix = string.Empty;
            return;
        }

        CompletionSuffix = SelectedResult.Title[QueryText.Length..];
    }
}

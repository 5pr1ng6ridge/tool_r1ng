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
            _ = RefreshResultsAsync();
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
            IsSearching = true;
            await Task.Delay(80, cancellationToken);
            var results = await _engine.SearchAsync(QueryText, cancellationToken);

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
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
                StatusText = Results.Count == 0 && !string.IsNullOrWhiteSpace(QueryText)
                    ? "No results"
                    : string.Empty;
            });
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
}

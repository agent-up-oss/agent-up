using System.Collections.ObjectModel;
using System.Diagnostics;
using AgentUp.Desktop.Features.Console.Controllers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Console.ViewModels;

public sealed class ConsoleViewModel : ReactiveObject
{
    internal const int DefaultDisplayLines = 2_000;
    internal const int MaxLines = 50_000;

    private readonly ConsoleController _console;
    private bool _isLoading;
    private bool _wasTruncated;
    private bool _hasHiddenLines;
    private bool _showAllLines;
    private string? _workspaceId;
    private string? _applicationName;
    private CancellationTokenSource? _activeLoad;
    private long _loadVersion;

    public ObservableCollection<string> Lines { get; } = [];

    public bool WasTruncated
    {
        get => _wasTruncated;
        private set => this.RaiseAndSetIfChanged(ref _wasTruncated, value);
    }

    public bool HasHiddenLines
    {
        get => _hasHiddenLines;
        private set => this.RaiseAndSetIfChanged(ref _hasHiddenLines, value);
    }

    public bool ShowAllLines
    {
        get => _showAllLines;
        private set => this.RaiseAndSetIfChanged(ref _showAllLines, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public ReactiveCommand<System.Reactive.Unit, System.Reactive.Unit> ShowMoreCommand { get; }

    public ConsoleViewModel(ConsoleController console)
    {
        _console = console;
        ShowMoreCommand = ReactiveCommand.Create(() => { ShowAllLines = true; });
    }

    public void Clear()
    {
        _activeLoad?.Cancel();
        _workspaceId = null;
        _applicationName = null;
        Lines.Clear();
        WasTruncated = false;
        HasHiddenLines = false;
        ShowAllLines = false;
        IsLoading = false;
    }

    public async Task LoadAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        _activeLoad?.Cancel();
        var version = ++_loadVersion;
        using var load = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeLoad = load;
        _workspaceId = workspaceId;
        _applicationName = appName;
        Lines.Clear();
        WasTruncated = false;
        HasHiddenLines = false;
        ShowAllLines = false;
        IsLoading = true;
        try
        {
            var lines = await _console.GetOutputAsync(workspaceId, appName, load.Token);
            if (version != _loadVersion
                || !string.Equals(workspaceId, _workspaceId, StringComparison.Ordinal)
                || !string.Equals(appName, _applicationName, StringComparison.Ordinal))
                return;

            WasTruncated = lines.Count > MaxLines;
            Lines.Clear();
            foreach (var line in lines.TakeLast(MaxLines))
                Lines.Add(line);
            HasHiddenLines = Lines.Count > DefaultDisplayLines;
        }
        catch (OperationCanceledException) when (load.Token.IsCancellationRequested)
        {
            Trace.TraceInformation("Superseded console load was cancelled.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            if (load.Token.IsCancellationRequested)
                return;

            Trace.TraceWarning(ex.Message);
        }
        finally
        {
            if (ReferenceEquals(_activeLoad, load))
                _activeLoad = null;

            if (version == _loadVersion)
                IsLoading = false;
        }
    }
}

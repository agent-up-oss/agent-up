using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reactive.Linq;
using AgentUp.Desktop.Features.Console.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Console.ViewModels;

public sealed class ConsoleViewModel : ReactiveObject
{
    internal const int DefaultDisplayLines = 2_000;
    internal const int MaxLines = 50_000;

    private readonly ConsoleApiClient _client;
    private bool _isLoading;
    private bool _wasTruncated;
    private bool _hasHiddenLines;
    private bool _showAllLines;

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

    public ConsoleViewModel(ConsoleApiClient client)
    {
        _client = client;
        ShowMoreCommand = ReactiveCommand.Create(() => { ShowAllLines = true; });
    }

    public void Clear()
    {
        Lines.Clear();
        WasTruncated = false;
        HasHiddenLines = false;
        ShowAllLines = false;
    }

    public async Task LoadAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        Lines.Clear();
        WasTruncated = false;
        HasHiddenLines = false;
        ShowAllLines = false;
        IsLoading = true;
        try
        {
            var lines = await _client.GetOutputAsync(workspaceId, appName, ct);
            WasTruncated = lines.Count > MaxLines;
            Lines.Clear();
            foreach (var line in lines.TakeLast(MaxLines))
                Lines.Add(line);
            HasHiddenLines = Lines.Count > DefaultDisplayLines;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Trace.TraceWarning(ex.Message);
        }
        finally
        {
            IsLoading = false;
        }
    }
}

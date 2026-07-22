using System.Collections.ObjectModel;
using System.Diagnostics;
using AgentUp.Desktop.Features.Console.Providers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Console.ViewModels;

public sealed class ConsoleViewModel : ReactiveObject
{
    internal const int MaxLines = 50_000;

    private readonly ConsoleApiClient _client;
    private bool _isLoading;
    private bool _wasTruncated;

    public ObservableCollection<string> Lines { get; } = [];

    public string ConsoleText => string.Join(Environment.NewLine, Lines);

    public bool WasTruncated
    {
        get => _wasTruncated;
        private set => this.RaiseAndSetIfChanged(ref _wasTruncated, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public ConsoleViewModel(ConsoleApiClient client)
    {
        _client = client;
    }

    public void Clear()
    {
        Lines.Clear();
        WasTruncated = false;
        this.RaisePropertyChanged(nameof(ConsoleText));
    }

    public async Task LoadAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        Lines.Clear();
        WasTruncated = false;
        IsLoading = true;
        try
        {
            var lines = await _client.GetOutputAsync(workspaceId, appName, ct);
            WasTruncated = lines.Count > MaxLines;
            Lines.Clear();
            foreach (var line in lines.TakeLast(MaxLines))
                Lines.Add(line);
            this.RaisePropertyChanged(nameof(ConsoleText));
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

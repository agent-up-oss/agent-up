using System.Collections.ObjectModel;
using AgentUp.Desktop.Features.Console.Http;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Console.ViewModels;

public sealed class ConsoleViewModel : ReactiveObject
{
    private readonly ConsoleApiClient _client;
    private bool _isLoading;

    public ObservableCollection<string> Lines { get; } = [];

    public bool IsLoading
    {
        get => _isLoading;
        set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public ConsoleViewModel(ConsoleApiClient client)
    {
        _client = client;
    }

    public void Clear() => Lines.Clear();

    public async Task LoadAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        Lines.Clear();
        IsLoading = true;
        try
        {
            var lines = await _client.GetOutputAsync(workspaceId, appName, ct);
            Lines.Clear();
            foreach (var line in lines)
                Lines.Add(line);
        }
        catch { /* output fetch failure is non-fatal */ }
        finally
        {
            IsLoading = false;
        }
    }
}

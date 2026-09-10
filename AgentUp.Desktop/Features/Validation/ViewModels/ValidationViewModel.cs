using System.Collections.ObjectModel;
using System.Text.Json;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Validation.Services;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Validation.ViewModels;

public sealed class ValidationViewModel(
    ValidationFlowApiClient client,
    ValidationFlowReplayService replay) : ReactiveObject
{
    private string? _activeFlowId;
    private string? _status;

    public ObservableCollection<ValidationFlowItemViewModel> Flows { get; } = [];
    public string? Status { get => _status; private set => this.RaiseAndSetIfChanged(ref _status, value); }
    public string? ActiveFlowId { get => _activeFlowId; private set => this.RaiseAndSetIfChanged(ref _activeFlowId, value); }

    public async Task LoadAsync(string workspaceId, string application, CancellationToken cancellationToken = default)
    {
        try
        {
            var flows = await client.ListAsync(workspaceId, application, cancellationToken);

            // A load the caller superseded must not repaint the panel for a selection the user
            // has already moved off, so check before touching the collection.
            cancellationToken.ThrowIfCancellationRequested();

            Flows.Clear();
            foreach (var flow in flows)
                Flows.Add(new ValidationFlowItemViewModel(flow, item => RunAsync(workspaceId, item)));

            Status = flows.Count == 0 ? "No checks recorded for this application." : null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Superseded by a newer selection; that load owns the panel state now.
            return;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            // A request timeout arrives as TaskCanceledException with our token unset, and a
            // malformed body as JsonException; both used to escape this fire-and-forget load
            // and leave the panel showing nothing at all.
            Flows.Clear();
            Status = ex.Message;
        }
    }

    private async Task RunAsync(string workspaceId, ValidationFlowItemViewModel item)
    {
        ActiveFlowId = item.Flow.Id;
        try
        {
            await replay.RunAsync(workspaceId, item.Flow.Id, item);
        }
        catch (OperationCanceledException)
        {
            item.CompleteFlow(false, "Validation replay was cancelled.");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            // Without this the flow sticks on Running with no message when the replay's HTTP
            // calls fail or the server answers with a body we cannot read.
            item.CompleteFlow(false, ex.Message);
        }
        finally
        {
            ActiveFlowId = null;
        }
    }
}

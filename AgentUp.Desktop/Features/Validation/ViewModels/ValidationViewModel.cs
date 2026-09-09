using System.Collections.ObjectModel;
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
            Flows.Clear();
            foreach (var flow in flows)
                Flows.Add(new ValidationFlowItemViewModel(flow, item => RunAsync(workspaceId, item)));

            Status = flows.Count == 0 ? "No checks recorded for this application." : null;
        }
        catch (HttpRequestException ex)
        {
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
        finally
        {
            ActiveFlowId = null;
        }
    }
}

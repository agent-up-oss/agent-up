using System.Collections.ObjectModel;
using AgentUp.Desktop.Features.Validation.Providers;
using ReactiveUI;
namespace AgentUp.Desktop.Features.Validation.ViewModels;
public sealed class ValidationViewModel(ValidationFlowApiClient client) : ReactiveObject
{
    private string? _status;
    public ObservableCollection<ValidationFlowItemViewModel> Flows { get; } = [];
    public string? Status { get => _status; private set => this.RaiseAndSetIfChanged(ref _status, value); }
    public async Task LoadAsync(string workspaceId, string application, CancellationToken ct = default)
    { try { var flows = await client.ListAsync(workspaceId, application, ct); Flows.Clear(); foreach (var flow in flows) Flows.Add(new(flow, item => RunAsync(workspaceId, item.Id))); Status = flows.Count == 0 ? "No checks recorded for this application." : null; } catch (HttpRequestException ex) { Status = ex.Message; } }
    private async Task RunAsync(string workspaceId, string id) { Status = "Playing validation…"; try { await client.RunAsync(workspaceId, id); Status = "Validation passed."; } catch (HttpRequestException ex) { Status = $"Validation failed: {ex.Message}"; } }
}

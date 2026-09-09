using System.ComponentModel;
using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Services;
using ModelContextProtocol.Server;
namespace AgentUp.Server.Features.Validation.Controllers;
[McpServerToolType]
public sealed class ValidationMcpTools(ValidationFlowService service)
{
    [McpServerTool(Name="save_validation_flow", Title="Save Behavioral Validation Flow")]
    [Description("Create or replace a replayable behavioral GUI flow. Start from the user's goal, infer routes and controls from application source or router files when available, and only inspect the live page for controls you cannot infer cheaply. Do not inspect every route before recording. Perform the journey once in the browser, then save the flow with stable selector fallbacks plus user-meaningful descriptions and visible expectations after each step. Reuse the returned id to edit or re-record a flow.")]
    public Task<SaveValidationFlowResult> Save([Description("Workspace id.")] string workspaceId, [Description("Complete flow. Supply an existing id to replace that flow with a newly edited or re-recorded version.")] SaveValidationFlowRequest flow, CancellationToken cancellationToken) => service.SaveAsync(workspaceId, flow, cancellationToken);

    [McpServerTool(Name="list_validation_flows", Title="List Behavioral Validation Flows")]
    [Description("List the persisted behavioral Playwright checks for one application so an agent can review, edit, re-record, export, or replay them.")]
    public Task<IReadOnlyList<ValidationFlow>> List(string workspaceId, string application, CancellationToken cancellationToken) => service.ListAsync(workspaceId, application, cancellationToken);

    [McpServerTool(Name="play_validation_flow", Title="Play Validation Flow")]
    [Description("Replay a saved flow in the workspace browser the user can watch. Desktop Play runs in the embedded WebView with staged mouse movement, half-second attention pings, navigation, and page-load waits.")]
    public Task<ValidationRunResult> Play(string workspaceId, string flowId, CancellationToken cancellationToken) => service.RunAsync(workspaceId, flowId, cancellationToken);

    [McpServerTool(Name="export_validation_flow", Title="Export Playwright Check")]
    [Description("Export a saved flow as a deterministic Playwright test using an AGENT_UP_BASE_URL supplied by local or headless CI.")]
    public Task<PlaywrightExport?> Export(string workspaceId, string flowId, CancellationToken cancellationToken) => service.ExportAsync(workspaceId, flowId, cancellationToken);

    [McpServerTool(Name="delete_validation_flow", Title="Delete Validation Flow")]
    public Task<bool> Delete(string workspaceId, string flowId, CancellationToken cancellationToken) => service.DeleteAsync(workspaceId, flowId, cancellationToken);
}

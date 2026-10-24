using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Interfaces;
using AgentUp.Desktop.Features.Validation.Models;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Features.Validation.Services;

public sealed class DesktopValidationLaunchService(
    ValidationFlowReplayService replay,
    ValidationFlowApiClient client,
    IValidationArtifactStore artifacts,
    IDesktopValidationReplayHost host,
    DesktopValidationEnvironment environment)
{
    public bool IsRequested => environment.ReadLaunch() is not null;

    public string? AdminPassword => environment.AdminPassword;

    public async Task<int?> RunIfRequestedAsync(
        object window,
        object viewModel,
        CancellationToken cancellationToken)
    {
        var request = environment.ReadLaunch();
        if (request is null)
            return null;

        return await RunAsync(window, viewModel, request, cancellationToken);
    }

    public async Task<int> RunAsync(
        object window,
        object viewModel,
        DesktopValidationLaunchRequest request,
        CancellationToken cancellationToken)
    {
        host.Attach(window, viewModel);
        var main = (MainViewModel)viewModel;
        var workspaceIds = SelectedWorkspaceIds(main, request.WorkspaceId);

        var flows = new List<ValidationFlowDto>();
        foreach (var workspaceId in workspaceIds)
            flows.AddRange(await client.ListAsync(workspaceId, DesktopValidationKeys.ApplicationName, cancellationToken));

        if (!string.IsNullOrWhiteSpace(request.FlowId))
            flows = flows.Where(flow => string.Equals(flow.Id, request.FlowId, StringComparison.Ordinal)).ToList();

        if (flows.Count == 0)
            return string.IsNullOrWhiteSpace(request.FlowId) ? 0 : 1;

        var failed = 0;
        foreach (var flow in flows)
        {
            var result = await replay.RunFlowAsync(
                flow,
                new SilentValidationReporter(),
                request.OutputRoot,
                cancellationToken);
            await artifacts.CopyLogAsync(
                artifacts.FlowDirectory(request.OutputRoot, flow.Id),
                "server.log",
                environment.ServerLogPath,
                cancellationToken);
            if (!result.Succeeded)
                failed++;
        }

        return failed == 0 ? 0 : 1;
    }

    private static List<string> SelectedWorkspaceIds(MainViewModel main, string? workspaceId)
    {
        var ids = main.Sidebar.Workspaces.Select(item => item.Id).ToList();
        if (string.IsNullOrWhiteSpace(workspaceId))
            return ids;

        return ids.Where(id => string.Equals(id, workspaceId, StringComparison.Ordinal)).ToList();
    }
}

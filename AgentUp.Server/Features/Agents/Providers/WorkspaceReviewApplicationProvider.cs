using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Restarts a reviewer's workspace through the Workspaces boundary, so the applications run the
/// revision just checked out rather than the one that was running before.
/// </summary>
public sealed class WorkspaceReviewApplicationProvider(WorkspaceLifecycleController lifecycle, WorkspaceQueryController workspaces)
    : IReviewApplicationHost
{
    public async Task<(IReadOnlyList<string> Addresses, string? Error)> RunAsync(string workspaceId, CancellationToken cancellationToken)
    {
        await lifecycle.StopAsync(workspaceId);
        var started = await lifecycle.StartAsync(workspaceId);
        if (!started.Found)
            return ([], "The workspace is not registered.");
        if (!started.Succeeded)
            return ([], started.Error ?? "The workspace did not start.");

        var addresses = (workspaces.GetById(workspaceId)?.Applications ?? [])
            .SelectMany(application => application.AllocatedPorts)
            .Where(port => string.Equals(port.Protocol, "http", StringComparison.OrdinalIgnoreCase))
            .Select(port => $"http://127.0.0.1:{port.AllocatedPort}/")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return (addresses, null);
    }
}

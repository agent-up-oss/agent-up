using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Restarts a reviewer's workspace through the Workspaces boundary, so the applications run the
/// revision just checked out rather than the one that was running before, and opens the first
/// HTTP application in the reviewer's own browser once the workspace is ready, so its first look
/// is at the revision rather than at an empty browser.
/// </summary>
public sealed class WorkspaceReviewApplicationProvider(
    WorkspaceLifecycleController lifecycle,
    WorkspaceQueryController workspaces,
    IReviewBrowser? browser = null,
    TimeSpan? readiness = null) : IReviewApplicationHost
{
    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(250);

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
        if (browser is null || addresses.Length == 0)
            return (addresses, null);

        // Start returns once processes launch; a page opened before the application answers
        // shows the reviewer a connection error instead of the revision.
        var deadline = DateTimeOffset.UtcNow + (readiness ?? TimeSpan.FromSeconds(90));
        while (workspaces.GetById(workspaceId)?.State == WorkspaceState.Starting && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(Poll, cancellationToken);
        if (workspaces.GetById(workspaceId)?.State is WorkspaceState.Failed)
            return ([], "The workspace failed to become ready.");

        var refusal = await browser.OpenAsync(workspaceId, addresses[0], cancellationToken);
        return refusal is null ? (addresses, null) : ([], $"The browser could not open {addresses[0]}: {refusal}");
    }

    public async Task StopAsync(string workspaceId, CancellationToken cancellationToken) =>
        await lifecycle.StopAsync(workspaceId);
}

using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Workspaces.Models;

internal static class WorkspaceStartEvaluation
{
    public static WorkspaceState StateFrom(IReadOnlyList<ApplicationInstance> applications)
    {
        if (applications.Any(app => app.State == ApplicationState.Starting))
            return WorkspaceState.Starting;

        var running = applications.Any(app =>
            app.State is ApplicationState.Running or ApplicationState.Checking or ApplicationState.Unhealthy);
        var failed = applications.Any(app => app.State == ApplicationState.Failed);
        if (running && failed)
            return WorkspaceState.Degraded;
        return running || !failed ? WorkspaceState.Running : WorkspaceState.Failed;
    }

    public static IReadOnlyList<WorkspaceApplicationFailure> Failures(Workspace workspace)
        => workspace.Applications
            .Where(app => app.State == ApplicationState.Failed)
            .Select(app => new WorkspaceApplicationFailure(app.Name, ReasonFor(app)))
            .ToList();

    public static string? Summarize(IReadOnlyList<WorkspaceApplicationFailure> failures)
        => failures.Count == 0
            ? null
            : string.Join("; ", failures.Select(failure => $"{failure.Name}: {failure.Reason}"));

    private static string ReasonFor(ApplicationInstance app)
    {
        if (!string.IsNullOrWhiteSpace(app.LastError))
            return app.LastError;
        if (app.CapabilityStatus is { CanRun: false, Messages.Count: > 0 })
            return string.Join(" ", app.CapabilityStatus.Messages);
        if (app.Kind == ApplicationKind.Desktop)
            return "Desktop applications currently require a Linux Agent-Up Server host.";
        return $"Application '{app.Name}' failed to start.";
    }
}

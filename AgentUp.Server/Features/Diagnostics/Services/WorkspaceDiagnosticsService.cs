using AgentUp.Server.Features.Applications.Controllers;
using AgentUp.Server.Features.Audit.Controllers;
using AgentUp.Server.Features.Audit.DTOs;
using AgentUp.Server.Features.Audit.Models;
using AgentUp.Server.Features.Diagnostics.DTOs;
using AgentUp.Server.Features.Processes.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Models;
using AgentUp.Server.Shared.Providers;

namespace AgentUp.Server.Features.Diagnostics.Services;

public sealed class WorkspaceDiagnosticsService(
    WorkspaceQueryController workspaces,
    ProcessesController processes,
    AppHealthController health,
    AuditController audit,
    ConsoleSecretRedactor redactor)
{
    private const int MaxLogLines = 1000;
    private const int MaxEntries = 500;

    public async Task<WorkspaceDiagnosticsDto?> GetAsync(
        string workspaceId,
        string? application,
        int logLimit,
        int entryLimit,
        CancellationToken cancellationToken)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return null;

        var normalizedLogLimit = Math.Clamp(logLimit <= 0 ? 200 : logLimit, 1, MaxLogLines);
        var normalizedEntryLimit = Math.Clamp(entryLimit <= 0 ? 100 : entryLimit, 1, MaxEntries);
        var selectedApplications = workspace.Applications
            .Where(app => string.IsNullOrWhiteSpace(application)
                || string.Equals(app.Name, application, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var applications = new List<ApplicationDiagnosticsDto>(selectedApplications.Count);
        foreach (var app in selectedApplications)
        {
            var lines = await processes.GetOutputAsync(workspace.Id, app.Name);
            applications.Add(new ApplicationDiagnosticsDto(
                app.Name,
                app.State.ToString(),
                AggregateHealth(health.GetPortHealth(workspace.Id, app.Name)),
                lines.Skip(Math.Max(0, lines.Count - normalizedLogLimit)).Select(redactor.Redact).ToList(),
                lines.Count > normalizedLogLimit));
        }

        var events = await audit.QueryAsync(
            new AuditEventQuery(workspace.Id, null, null, null, null, null, null, null, null, null, MaxEntries),
            cancellationToken);
        var entries = events
            .Where(IsDiagnosticEvent)
            .Where(evt => string.IsNullOrWhiteSpace(application)
                || string.Equals(GetApplication(evt.Details), application, StringComparison.OrdinalIgnoreCase))
            .Take(normalizedEntryLimit)
            .Select(ToEntry)
            .ToList();

        return new WorkspaceDiagnosticsDto(
            workspace.Id,
            workspace.DisplayName,
            workspace.State.ToString(),
            health.GetWorkspaceHealth(workspace.Id),
            DateTimeOffset.UtcNow,
            applications,
            entries);
    }

    private static bool IsDiagnosticEvent(AuditEvent evt)
        => evt.Kind.Equals("health", StringComparison.OrdinalIgnoreCase)
           || evt.Action.Contains("error", StringComparison.OrdinalIgnoreCase)
           || evt.Action.Contains("exception", StringComparison.OrdinalIgnoreCase)
           || evt.Action.Contains("network", StringComparison.OrdinalIgnoreCase)
           || ((evt.Kind.Equals("frontend", StringComparison.OrdinalIgnoreCase)
                || evt.Kind.Equals("browser", StringComparison.OrdinalIgnoreCase))
               && !IsResolved(evt.Outcome))
           || (evt.Action.Equals("application_console_line", StringComparison.Ordinal)
               && evt.Details.TryGetValue("stream", out var stream)
               && stream.Equals("stderr", StringComparison.OrdinalIgnoreCase));

    private static DiagnosticEntryDto ToEntry(AuditEvent evt)
    {
        var state = IsResolved(evt.Outcome) ? "resolved" : "active";
        var category = evt.Kind.Equals("frontend", StringComparison.OrdinalIgnoreCase)
            ? CategoryForFrontend(evt.Action)
            : evt.Kind.ToLowerInvariant();
        var severity = state == "resolved" ? "info" : category == "health" ? "warning" : "error";
        return new DiagnosticEntryDto(
            evt.EventId,
            evt.Timestamp,
            category,
            severity,
            state,
            evt.Source,
            evt.Action,
            GetApplication(evt.Details),
            GetDetail(evt.Details, "browserSessionId", "sessionId"),
            GetDetail(evt.Details, "message", "error", "url") ?? evt.Action,
            evt.Details);
    }

    private static string CategoryForFrontend(string action)
        => action.Contains("network", StringComparison.OrdinalIgnoreCase)
            || action.Contains("request", StringComparison.OrdinalIgnoreCase)
            ? "network"
            : action.Contains("exception", StringComparison.OrdinalIgnoreCase)
                || action.Contains("error", StringComparison.OrdinalIgnoreCase)
                ? "javascript"
                : "frontend";

    private static bool IsResolved(string outcome)
        => outcome.Equals("success", StringComparison.OrdinalIgnoreCase)
           || outcome.Equals("healthy", StringComparison.OrdinalIgnoreCase)
           || outcome.Equals("resolved", StringComparison.OrdinalIgnoreCase)
           || outcome.Equals("recovered", StringComparison.OrdinalIgnoreCase);

    private static string? GetApplication(IReadOnlyDictionary<string, string> details)
        => GetDetail(details, "application", "applicationName", "appName");

    private static string? GetDetail(IReadOnlyDictionary<string, string> details, params string[] names)
    {
        foreach (var name in names)
            if (details.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;
        return null;
    }

    private static string? AggregateHealth(IReadOnlyList<PortHealthChange>? ports)
    {
        if (ports is null || ports.Count == 0) return null;
        if (ports.Any(port => port.State == "Unhealthy")) return "Unhealthy";
        if (ports.Any(port => port.State == "Checking")) return "Checking";
        return "Healthy";
    }
}

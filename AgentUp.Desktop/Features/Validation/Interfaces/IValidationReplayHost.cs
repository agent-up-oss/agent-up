namespace AgentUp.Desktop.Features.Validation.Interfaces;

public interface IValidationReplayHost
{
    string? ResolveApplicationOrigin(string workspaceId, string application);

    Task<bool> PrepareViewportAsync(string workspaceId, string application, string url, CancellationToken cancellationToken);

    Task<string?> EvalAsync(string workspaceId, string script, CancellationToken cancellationToken);

    Task NavigateAsync(string workspaceId, string url, CancellationToken cancellationToken);

    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

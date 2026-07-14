namespace AgentUp.Desktop.Features.FirstRun.Services;

public interface IFirstRunTutorialChecks
{
    Task<FirstRunCheckResult> CheckDockerAsync(CancellationToken cancellationToken = default);

    Task<FirstRunCheckResult> CheckNodeAsync(CancellationToken cancellationToken = default);

    Task<FirstRunCheckResult> CreateJavaScriptSampleAsync(string projectDirectory, CancellationToken cancellationToken = default);

    Task<FirstRunCheckResult> CheckJavaScriptProjectFilesAsync(string projectDirectory, CancellationToken cancellationToken = default);

    Task<FirstRunCheckResult> CreateAgentUpJsonAsync(string projectDirectory, CancellationToken cancellationToken = default);

    Task<FirstRunCheckResult> CheckAgentUpJsonAsync(string projectDirectory, CancellationToken cancellationToken = default);

    Task<FirstRunCheckResult> StartJavaScriptWorkspaceAsync(string projectDirectory, CancellationToken cancellationToken = default);

    Task<FirstRunCheckResult> CheckJavaScriptWorkspaceAsync(string projectDirectory, CancellationToken cancellationToken = default);

    Task<FirstRunCheckResult> CheckDuplicatedJavaScriptWorkspacesAsync(string projectDirectory, CancellationToken cancellationToken = default);
}

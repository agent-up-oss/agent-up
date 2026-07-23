namespace AgentUp.Desktop.Features.FirstRun.Services;

internal delegate Task<(int ExitCode, string Stdout, string Stderr)> ProcessRunner(
    string fileName,
    string arguments,
    TimeSpan timeout,
    CancellationToken cancellationToken,
    string? workingDirectory = null);

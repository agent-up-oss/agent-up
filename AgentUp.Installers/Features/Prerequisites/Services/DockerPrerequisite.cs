namespace AgentUp.Installers.Features.Prerequisites.Services;

public enum DockerStatusKind
{
    NotInstalled,
    DaemonNotRunning,
    Inaccessible,
    UnsupportedVersion,
    Operational
}

public sealed record DockerStatus(
    DockerStatusKind Kind,
    string Title,
    string Detail,
    Version? Version = null)
{
    public bool CanContinue => Kind == DockerStatusKind.Operational;
}

public sealed record ProcessResult(int ExitCode, string Stdout, string Stderr);

public interface ICommandRunner
{
    Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}

public sealed class DockerPrerequisite
{
    private readonly ICommandRunner _commands;
    private readonly Version _minimumVersion;

    public DockerPrerequisite(ICommandRunner commands, Version minimumVersion)
    {
        _commands = commands;
        _minimumVersion = minimumVersion;
    }

    public async Task<DockerStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        ProcessResult version;
        try
        {
            version = await _commands.RunAsync("docker", "version --format {{.Client.Version}}", cancellationToken);
        }
        catch (FileNotFoundException)
        {
            return new DockerStatus(DockerStatusKind.NotInstalled, "Docker is not installed",
                "Install Docker, then retry prerequisite validation.");
        }

        if (version.ExitCode != 0)
        {
            return new DockerStatus(DockerStatusKind.NotInstalled, "Docker is not installed",
                FirstNonEmpty(version.Stderr, version.Stdout, "The Docker CLI could not be started."));
        }

        var parsedVersion = ParseVersion(version.Stdout);
        if (parsedVersion is null)
        {
            return new DockerStatus(DockerStatusKind.NotInstalled, "Docker version could not be verified",
                FirstNonEmpty(version.Stdout, version.Stderr, "Docker did not report a client version."));
        }

        if (parsedVersion < _minimumVersion)
        {
            return new DockerStatus(DockerStatusKind.UnsupportedVersion, "Docker is too old",
                $"Docker {parsedVersion} is installed, but Agent-Up requires Docker {_minimumVersion} or newer.",
                parsedVersion);
        }

        var info = await _commands.RunAsync("docker", "info", cancellationToken);
        if (info.ExitCode == 0)
        {
            return new DockerStatus(DockerStatusKind.Operational, "Docker is operational",
                $"Docker {parsedVersion} is installed and the daemon is responding.", parsedVersion);
        }

        var error = FirstNonEmpty(info.Stderr, info.Stdout, "Docker daemon did not respond.");
        if (LooksLikePermissionError(error))
        {
            return new DockerStatus(DockerStatusKind.Inaccessible, "Docker is inaccessible",
                error, parsedVersion);
        }

        return new DockerStatus(DockerStatusKind.DaemonNotRunning, "Docker daemon is not running",
            error, parsedVersion);
    }

    private static Version? ParseVersion(string text)
    {
        var candidate = text.Trim().TrimStart('v', 'V');
        var token = candidate.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return Version.TryParse(token, out var version) ? version : null;
    }

    private static bool LooksLikePermissionError(string text)
        => text.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
           || text.Contains("access is denied", StringComparison.OrdinalIgnoreCase)
           || text.Contains("Got permission denied", StringComparison.OrdinalIgnoreCase);

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? "";
}

using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;

namespace AgentUp.Installers.Features.PrerequisiteChecks.Providers;

public sealed class DockerPrerequisiteProvider : IDockerPrerequisiteProvider
{
    private readonly ICommandRunner _commands;

    public DockerPrerequisiteProvider(ICommandRunner commands)
    {
        _commands = commands;
    }

    public async Task<DockerStatus> CheckAsync(Version minimumVersion, CancellationToken cancellationToken = default)
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

        if (parsedVersion < minimumVersion)
        {
            return new DockerStatus(DockerStatusKind.UnsupportedVersion, "Docker is too old",
                $"Docker {parsedVersion} is installed, but Agent-Up requires Docker {minimumVersion} or newer.",
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

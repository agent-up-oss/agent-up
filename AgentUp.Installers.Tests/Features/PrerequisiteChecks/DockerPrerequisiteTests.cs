using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Interfaces;
using AgentUp.Installers.Features.UbuntuInstallation.Interfaces;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Tests.Features.PrerequisiteChecks;

[TestFixture]
public class DockerPrerequisiteTests
{
    [Test]
    public async Task CheckAsync_reportsNotInstalled_whenDockerCommandIsMissing()
    {
        var check = new DockerPrerequisite(new FakeCommandRunner { ThrowOnDockerVersion = true }, new Version(27, 0, 0));

        var result = await check.CheckAsync();

        Assert.That(result.Kind, Is.EqualTo(DockerStatusKind.NotInstalled));
        Assert.That(result.CanContinue, Is.False);
    }

    [Test]
    public async Task CheckAsync_reportsUnsupportedVersion_whenClientVersionIsTooOld()
    {
        var check = new DockerPrerequisite(new FakeCommandRunner
        {
            VersionResult = new ProcessResult(0, "26.1.0", "")
        }, new Version(27, 0, 0));

        var result = await check.CheckAsync();

        Assert.That(result.Kind, Is.EqualTo(DockerStatusKind.UnsupportedVersion));
        Assert.That(result.Version, Is.EqualTo(new Version(26, 1, 0)));
    }

    [Test]
    public async Task CheckAsync_reportsDaemonNotRunning_whenDockerInfoCannotReachDaemon()
    {
        var check = new DockerPrerequisite(new FakeCommandRunner
        {
            VersionResult = new ProcessResult(0, "27.0.0", ""),
            InfoResult = new ProcessResult(1, "", "Cannot connect to the Docker daemon")
        }, new Version(27, 0, 0));

        var result = await check.CheckAsync();

        Assert.That(result.Kind, Is.EqualTo(DockerStatusKind.DaemonNotRunning));
    }

    [Test]
    public async Task CheckAsync_reportsInaccessible_whenDockerInfoReturnsPermissionError()
    {
        var check = new DockerPrerequisite(new FakeCommandRunner
        {
            VersionResult = new ProcessResult(0, "27.0.0", ""),
            InfoResult = new ProcessResult(1, "", "permission denied while trying to connect")
        }, new Version(27, 0, 0));

        var result = await check.CheckAsync();

        Assert.That(result.Kind, Is.EqualTo(DockerStatusKind.Inaccessible));
    }

    [Test]
    public async Task CheckAsync_reportsOperational_whenVersionAndInfoSucceed()
    {
        var check = new DockerPrerequisite(new FakeCommandRunner
        {
            VersionResult = new ProcessResult(0, "27.0.0", ""),
            InfoResult = new ProcessResult(0, "Server Version: 27.0.0", "")
        }, new Version(27, 0, 0));

        var result = await check.CheckAsync();

        Assert.That(result.Kind, Is.EqualTo(DockerStatusKind.Operational));
        Assert.That(result.CanContinue, Is.True);
    }

    private sealed class FakeCommandRunner : ICommandRunner
    {
        public bool ThrowOnDockerVersion { get; init; }
        public ProcessResult VersionResult { get; init; } = new(0, "27.0.0", "");
        public ProcessResult InfoResult { get; init; } = new(0, "", "");

        public Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
        {
            if (fileName == "docker" && arguments.StartsWith("version", StringComparison.Ordinal))
            {
                if (ThrowOnDockerVersion)
                    throw new FileNotFoundException("docker");

                return Task.FromResult(VersionResult);
            }

            if (fileName == "docker" && arguments == "info")
                return Task.FromResult(InfoResult);

            throw new InvalidOperationException($"{fileName} {arguments}");
        }
    }
}

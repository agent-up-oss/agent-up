using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.NixOsInstallation.Interfaces;
using AgentUp.Installers.Features.NixOsInstallation.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Services;

namespace AgentUp.Installers.Tests.Features.NixOsInstallation;

[TestFixture]
public sealed class NixOsInstallerPlatformAdapterTests
{
    [Test]
    public async Task GetComponentStatusAsync_reportsInstalledWhenExecutableIsOnPath()
    {
        var adapter = new NixOsInstallerPlatformAdapter(
            new Lookup(("agent-up", "/nix/store/agent-up/bin/agent-up")),
            Docker());

        var status = await adapter.GetComponentStatusAsync(InstallerComponentTarget.Cli, Session());

        Assert.That(adapter.SupportsInstallActions, Is.False);
        Assert.That(status.Kind, Is.EqualTo(InstallerComponentStatusKind.Installed));
        Assert.That(status.Message, Does.Contain("/nix/store/agent-up/bin/agent-up"));
    }

    [Test]
    public async Task GetComponentStatusAsync_reportsNotInstalledWhenExecutableIsMissing()
    {
        var adapter = new NixOsInstallerPlatformAdapter(new Lookup(), Docker());

        var status = await adapter.GetComponentStatusAsync(InstallerComponentTarget.Desktop, Session());

        Assert.That(status.Kind, Is.EqualTo(InstallerComponentStatusKind.NotInstalled));
        Assert.That(status.Message, Does.Contain("NixOS or Home Manager"));
    }

    [Test]
    public async Task ExecuteComponentActionAsync_neverInstallsAndReportsDeclarativeManagement()
    {
        var adapter = new NixOsInstallerPlatformAdapter(new Lookup(), Docker());

        var progress = new List<InstallProgress>();
        await foreach (var item in adapter.ExecuteComponentActionAsync(
                           InstallerComponentTarget.Server,
                           InstallerComponentAction.Install,
                           Session()))
        {
            progress.Add(item);
        }

        Assert.That(progress, Has.Count.EqualTo(1));
        Assert.That(progress[0].Message, Does.Contain("disabled on NixOS"));
    }

    private static InstallerSession Session()
        => InstallerSession.CreateDefault(
            ProductManifest.AgentUp(),
            new Version(1, 2, 3),
            "/opt/agent-up",
            PayloadSelection.Bundled(new Version(1, 2, 3)));

    private static DockerPrerequisite Docker()
        => new(new DockerProvider(), new Version(27, 0, 0));

    private sealed class Lookup(params (string Name, string Path)[] entries) : INixOsExecutableLookup
    {
        private readonly Dictionary<string, string> _entries = entries.ToDictionary(
            entry => entry.Name,
            entry => entry.Path,
            StringComparer.Ordinal);

        public string? Find(string executableName)
            => _entries.GetValueOrDefault(executableName);
    }

    private sealed class DockerProvider : IDockerPrerequisiteProvider
    {
        public Task<DockerStatus> CheckAsync(Version minimumVersion, CancellationToken cancellationToken = default)
            => Task.FromResult(new DockerStatus(
                DockerStatusKind.NotInstalled,
                "Docker was not found",
                "Docker is managed independently.",
                null));
    }
}

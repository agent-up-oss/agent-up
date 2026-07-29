using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.NixOsInstallation.Interfaces;
using AgentUp.Installers.Features.NixOsInstallation.Providers;
using AgentUp.Installers.Features.PrerequisiteChecks.Interfaces;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;
using AgentUp.Installers.Features.PrerequisiteChecks.Models;

namespace AgentUp.Installers.Tests.Features.NixOsInstallation.Provider;

[TestFixture]
public sealed class NixOsInstallerPlatformAdapterTests
{
    [Test]
    public async Task GetComponentStatusAsync_reportsInstalledWhenExecutableIsOnPath()
    {
        var adapter = new NixOsInstallerPlatformAdapter(
            new Lookup(("agent-up", "/nix/store/agent-up/bin/agent-up")),
            Docker());

        var status = await adapter.GetComponentStatusAsync(ProductComponent.Cli, Session());

        Assert.That(adapter.SupportsInstallActions, Is.False);
        Assert.That(status.Kind, Is.EqualTo(InstallerComponentStatusKind.Installed));
        Assert.That(status.Message, Does.Contain("/nix/store/agent-up/bin/agent-up"));
    }

    [Test]
    public async Task GetComponentStatusAsync_reportsNotInstalledWhenExecutableIsMissing()
    {
        var adapter = new NixOsInstallerPlatformAdapter(new Lookup(), Docker());

        var status = await adapter.GetComponentStatusAsync(ProductComponent.Desktop, Session());

        Assert.That(status.Kind, Is.EqualTo(InstallerComponentStatusKind.NotInstalled));
        Assert.That(status.Message, Does.Contain("NixOS or Home Manager"));
    }

    [Test]
    public async Task GetComponentStatusAsync_forNonAgentUpManifest_usesProductSlugAndProductName()
    {
        var adapter = new NixOsInstallerPlatformAdapter(new Lookup(), Docker());

        var status = await adapter.GetComponentStatusAsync(ProductComponent.Cli, AcmeStudioSession());

        Assert.Multiple(() =>
        {
            Assert.That(status.Kind, Is.EqualTo(InstallerComponentStatusKind.NotInstalled));
            Assert.That(status.Message, Does.Contain("acme-studio"));
            Assert.That(status.Message, Does.Contain("Acme Studio"));
            Assert.That(status.Message, Does.Not.Contain("agent-up"));
            Assert.That(status.Message, Does.Not.Contain("Agent-Up"));
        });
    }

    [Test]
    public async Task ExecuteComponentActionAsync_neverInstallsAndReportsDeclarativeManagement()
    {
        var adapter = new NixOsInstallerPlatformAdapter(new Lookup(), Docker());

        var progress = new List<InstallProgress>();
        await foreach (var item in adapter.ExecuteComponentActionAsync(
                           ProductComponent.Server,
                           InstallerComponentAction.Install,
                           Session()))
        {
            progress.Add(item);
        }

        Assert.That(progress, Has.Count.EqualTo(1));
        Assert.That(progress[0].Message, Does.Contain("disabled on NixOS"));
    }

    [Test]
    public async Task ValidateInstalledStateAsync_forNonAgentUpManifest_derivesEveryExecutableNameFromManifest()
    {
        var adapter = new NixOsInstallerPlatformAdapter(
            new Lookup(
                ("acme-studio-desktop", "/nix/store/acme/bin/acme-studio-desktop"),
                ("acme-studio-server", "/nix/store/acme/bin/acme-studio-server"),
                ("acme-studio", "/nix/store/acme/bin/acme-studio"),
                ("acme-studio-tray", "/nix/store/acme/bin/acme-studio-tray")),
            Docker());

        var report = await adapter.ValidateInstalledStateAsync(AcmeStudioSession());
        var messages = string.Join(" ", report.Findings.Select(finding => finding.Message));
        var cliMessage = report.Findings.Single(finding => finding.Code == "cli.path").Message;

        Assert.Multiple(() =>
        {
            Assert.That(report.Succeeded, Is.True);
            Assert.That(messages, Does.Contain("acme-studio-desktop"));
            Assert.That(messages, Does.Contain("acme-studio-server"));
            Assert.That(messages, Does.Contain("acme-studio-tray"));
            Assert.That(cliMessage, Does.Contain("/nix/store/acme/bin/acme-studio"));
            Assert.That(cliMessage, Does.Not.Contain("acme-studio-desktop"));
            Assert.That(cliMessage, Does.Not.Contain("acme-studio-server"));
            Assert.That(cliMessage, Does.Not.Contain("acme-studio-tray"));
            Assert.That(messages, Does.Not.Contain("agent-up"));
        });
    }

    private static InstallerSession Session()
        => InstallerSession.CreateDefault(
            ProductManifest.AgentUp(),
            new Version(1, 2, 3),
            "/opt/agent-up",
            PayloadSelection.Bundled(new Version(1, 2, 3)));

    private static InstallerSession AcmeStudioSession()
        => InstallerSession.CreateDefault(
            new ProductManifest("Acme Studio", "acme-studio", "ACMESTUDIO")
            {
                Components =
                [
                    ProductComponent.Desktop,
                    ProductComponent.Server,
                    ProductComponent.Cli,
                    new("tray", "Tray")
                ]
            },
            new Version(1, 2, 3),
            "/opt/acme-studio",
            PayloadSelection.Bundled("Acme Studio", new Version(1, 2, 3)));

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

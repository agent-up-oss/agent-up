using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Models;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Providers;

namespace AgentUp.Capabilities.Docker.Tests.Features.DockerCapability.Provider;

[TestFixture]
public sealed class DockerVersionProviderTests
{
    private string? _previousInventoryPath;

    [SetUp]
    public void SetUp()
    {
        _previousInventoryPath = Environment.GetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable);
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.json"));
    }

    [TearDown]
    public void TearDown()
        => Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, _previousInventoryPath);

    [Test]
    public async Task DiscoverAsync_mergesCliAndUbuntuAptDiscovery()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("docker", "version --format {{.Client.Version}}")] = new CapabilityCommandResult(0, "27.3.1\n", "");
        commands.Results[("apt-cache", "policy docker-ce")] = new CapabilityCommandResult(0, "Installed: 5:27.3.1-1~ubuntu\n", "");

        var versions = await new DockerVersionProvider(new CapabilityInventoryFileProvider(), commands, "ubuntu").DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Select(version => version.Location), Does.Contain("docker"));
        Assert.That(versions.Select(version => version.Location), Does.Contain("apt:docker-ce"));
    }

    [Test]
    public async Task DiscoverAsync_detectsHomebrewDockerPackage()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("brew", "list --versions docker")] = new CapabilityCommandResult(0, "docker 27.3.1\n", "");

        var versions = await new DockerVersionProvider(new CapabilityInventoryFileProvider(), commands, "macos").DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Single(version => version.Location == "brew:docker").Version, Is.EqualTo("27.3.1"));
    }

    [Test]
    public async Task DiscoverAsync_detectsChocolateyAndWingetDockerDesktop()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("choco", "list --local-only --exact docker-desktop")] = new CapabilityCommandResult(0, "docker-desktop 4.35.1\n", "");
        commands.Results[("winget", "list --id Docker.DockerDesktop --exact")] = new CapabilityCommandResult(0, "Docker Desktop Docker.DockerDesktop 4.35.1\n", "");

        var versions = await new DockerVersionProvider(new CapabilityInventoryFileProvider(), commands, "windows").DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Select(version => version.Location), Does.Contain("choco:docker-desktop"));
        Assert.That(versions.Select(version => version.Location), Does.Contain("winget:Docker.DockerDesktop"));
    }

    private sealed class RecordingCommandRunner : ICapabilityCommandRunner
    {
        public Dictionary<(string FileName, string Arguments), CapabilityCommandResult> Results { get; } = [];

        public Task<CapabilityCommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            var key = (fileName, string.Join(" ", arguments));
            return Task.FromResult(Results.GetValueOrDefault(key, new CapabilityCommandResult(127, "", "missing")));
        }
    }
}

using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Interfaces;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Services;

namespace AgentUp.Capabilities.Docker.Tests.Features.DockerCapability.Unit;

[TestFixture]
public sealed class DockerCapabilityAdapterTests
{
    [Test]
    public async Task ValidateAsync_requiresDockerCliDiscovery()
    {
        var adapter = new DockerCapabilityAdapter(new FakeDockerVersionProvider());
        var declaration = new CapabilityDeclaration(
            "postgres",
            "docker",
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["image"] = "postgres:17" });

        var result = await adapter.ValidateAsync(declaration, await adapter.DiscoverAsync(CancellationToken.None), CancellationToken.None);

        Assert.That(result.CanRun, Is.False);
        Assert.That(result.Messages.Single().Code, Is.EqualTo("docker.cli.missing"));
    }

    [Test]
    public async Task ValidateAsync_acceptsDiscoveredDockerAndImage()
    {
        var adapter = new DockerCapabilityAdapter(new FakeDockerVersionProvider("27.0.0"));
        var declaration = new CapabilityDeclaration(
            "postgres",
            "docker",
            new Dictionary<string, string>(),
            new Dictionary<string, string> { ["image"] = "postgres:17" });

        var result = await adapter.ValidateAsync(declaration, await adapter.DiscoverAsync(CancellationToken.None), CancellationToken.None);

        Assert.That(result.CanRun, Is.True);
    }

    private sealed class FakeDockerVersionProvider(params string[] versions) : IDockerVersionProvider
    {
        public Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<CapabilityInstalledVersion>>(versions
                .Select(version => new CapabilityInstalledVersion("docker", version, "docker", CapabilityVersionSource.System, false))
                .ToList());
    }
}

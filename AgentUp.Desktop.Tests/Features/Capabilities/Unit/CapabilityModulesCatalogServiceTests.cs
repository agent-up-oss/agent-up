using AgentUp.Desktop.Features.Capabilities.DTOs;
using AgentUp.Desktop.Features.Capabilities.Interfaces;
using AgentUp.Desktop.Features.Capabilities.Services;

namespace AgentUp.Desktop.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class CapabilityModulesCatalogServiceTests
{
    [Test]
    public async Task ListAsync_returnsTheProviderCatalog()
    {
        var client = new FakeCapabilityModulesApiProvider
        {
            Modules = [DotnetModule(enabled: true)]
        };

        var listed = await new CapabilityModulesCatalogService(client).ListAsync();

        Assert.That(listed.Single().Id, Is.EqualTo("dotnet"));
        Assert.That(client.ListCalls, Is.EqualTo(1));
    }

    [Test]
    public async Task EnableAsync_forwardsThePackageIdentity()
    {
        var client = new FakeCapabilityModulesApiProvider { Modules = [DotnetModule(enabled: false)] };
        var service = new CapabilityModulesCatalogService(client);

        var enabled = await service.EnableAsync("dotnet", "1.0.0");

        Assert.That(enabled.Enabled, Is.True);
        Assert.That(client.EnabledId, Is.EqualTo("dotnet"));
        Assert.That(client.EnabledVersion, Is.EqualTo("1.0.0"));
    }

    [Test]
    public async Task DisableAsync_forwardsThePackageId()
    {
        var client = new FakeCapabilityModulesApiProvider { Modules = [DotnetModule(enabled: true)] };

        var disabled = await new CapabilityModulesCatalogService(client).DisableAsync("dotnet");

        Assert.That(disabled.Enabled, Is.False);
        Assert.That(client.DisabledId, Is.EqualTo("dotnet"));
    }

    internal static CapabilityModuleDto DotnetModule(bool enabled)
        => new("dotnet", "1.0.0", ".NET", "agent-up", "runtime", enabled, enabled ? "ready" : "disabled", enabled, enabled ? [] : ["not enabled"]);
}

internal sealed class FakeCapabilityModulesApiProvider : ICapabilityModulesApiProvider
{
    public IReadOnlyList<CapabilityModuleDto> Modules { get; set; } = [];

    public int ListCalls { get; private set; }

    public string? EnabledId { get; private set; }

    public string? EnabledVersion { get; private set; }

    public string? DisabledId { get; private set; }

    public Task<IReadOnlyList<CapabilityModuleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        ListCalls++;
        return Task.FromResult(Modules);
    }

    public Task<CapabilityModuleDto> EnableAsync(string id, string? version, CancellationToken cancellationToken = default)
    {
        EnabledId = id;
        EnabledVersion = version;
        return Task.FromResult(CapabilityModulesCatalogServiceTests.DotnetModule(true));
    }

    public Task<CapabilityModuleDto> DisableAsync(string id, CancellationToken cancellationToken = default)
    {
        DisabledId = id;
        return Task.FromResult(CapabilityModulesCatalogServiceTests.DotnetModule(false));
    }
}

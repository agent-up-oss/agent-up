using AgentUp.Desktop.Features.Capabilities.DTOs;
using AgentUp.Desktop.Features.Capabilities.Interfaces;
using AgentUp.Desktop.Features.Capabilities.Services;
using AgentUp.Desktop.Tests.Support;

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
    public async Task EnableAsync_forwardsTheCapabilityId()
    {
        var client = new FakeCapabilityModulesApiProvider { Modules = [DotnetModule(enabled: false)] };
        var service = new CapabilityModulesCatalogService(client);

        var enabled = await service.EnableAsync("dotnet");

        Assert.That(enabled.Enabled, Is.True);
        Assert.That(client.EnabledId, Is.EqualTo("dotnet"));
    }

    [Test]
    public async Task UpdateSettingsAsync_forwardsOnlyTheGivenValues()
    {
        var client = new FakeCapabilityModulesApiProvider();

        await new CapabilityModulesCatalogService(client).UpdateSettingsAsync("opencode", new Dictionary<string, string> { ["endpoint"] = "http://a.test" });

        Assert.That(client.SavedId, Is.EqualTo("opencode"));
        Assert.That(client.SavedValues!["endpoint"], Is.EqualTo("http://a.test"));
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
        => enabled ? DesktopDomain.Capability().Enabled().Build() : DesktopDomain.Capability().Build();
}

internal sealed class FakeCapabilityModulesApiProvider : ICapabilityModulesApiProvider
{
    public IReadOnlyList<CapabilityModuleDto> Modules { get; set; } = [];

    public int ListCalls { get; private set; }

    public string? EnabledId { get; private set; }

    public string? SavedId { get; private set; }

    public IReadOnlyDictionary<string, string>? SavedValues { get; private set; }

    public string? DisabledId { get; private set; }

    public Task<IReadOnlyList<CapabilityModuleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        ListCalls++;
        return Task.FromResult(Modules);
    }

    public Task<CapabilityModuleDto> EnableAsync(string id, CancellationToken cancellationToken = default)
    {
        EnabledId = id;
        return Task.FromResult(CapabilityModulesCatalogServiceTests.DotnetModule(true));
    }

    public Task<CapabilityModuleDto> DisableAsync(string id, CancellationToken cancellationToken = default)
    {
        DisabledId = id;
        return Task.FromResult(CapabilityModulesCatalogServiceTests.DotnetModule(false));
    }

    public Task<CapabilityModuleDto> UpdateSettingsAsync(
        string id,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        SavedId = id;
        SavedValues = values;
        return Task.FromResult(CapabilityModulesCatalogServiceTests.DotnetModule(true));
    }
}

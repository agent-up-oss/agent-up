using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Tests.Fake;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class ConfiguredAutonomySettingsTests
{
    [Test]
    public void TryEnable_refusesUntilAHostConnectionIsAuthorized()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var autonomy = new ConfiguredAutonomySettings(EmptyConfiguration(), settings);

        var enabled = autonomy.TryEnable(out var refusal);

        Assert.Multiple(() =>
        {
            Assert.That(enabled, Is.False);
            Assert.That(refusal, Does.Contain("host connection"));
            Assert.That(autonomy.ServerEnabled, Is.False);
        });
    }

    [Test]
    public void TryEnable_persistsUntilDisabled()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        settings.Save(JsonAgentWorkspaceSettingsRepository.Empty("ws") with { HostConnectionAuthorized = true });
        var autonomy = new ConfiguredAutonomySettings(EmptyConfiguration(), settings);

        Assert.That(autonomy.TryEnable(out _), Is.True);
        Assert.That(autonomy.ServerEnabled, Is.True);
        autonomy.Disable();
        Assert.That(autonomy.ServerEnabled, Is.False);
    }

    [Test]
    public void ServerEnabled_readsTheEnvironmentOverride()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AGENTUP_AUTONOMY_ENABLED"] = "true" })
            .Build();
        var autonomy = new ConfiguredAutonomySettings(configuration, new InMemoryAgentWorkspaceSettingsRepository());

        Assert.That(autonomy.ServerEnabled, Is.True);
    }

    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().AddInMemoryCollection().Build();
}

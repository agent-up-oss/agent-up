using AgentUp.Server.Features.Agents.Models;
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
            Assert.That(refusal, Is.EqualTo(AutonomyPreconditions.Refusal(AutonomyPreconditions.HostConnectionAuthorized)));
            Assert.That(autonomy.ServerEnabled, Is.False);
        });
    }

    [Test]
    public void TryEnable_storesEnablementUntilDisabled()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        settings.Save(JsonAgentWorkspaceSettingsRepository.Empty("ws") with { HostConnectionAuthorized = true });
        var autonomy = new ConfiguredAutonomySettings(EmptyConfiguration(), settings);

        Assert.That(autonomy.TryEnable(out _), Is.True);
        Assert.That(autonomy.ServerEnabled, Is.True);
        Assert.That(settings.ServerEnabled, Is.True);
        autonomy.Disable();
        Assert.That(autonomy.ServerEnabled, Is.False);
        Assert.That(settings.ServerEnabled, Is.False);
    }

    [Test]
    public void ServerEnabled_readsTheEnvironmentOverrideAheadOfStoredFalse()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AGENTUP_AUTONOMY_ENABLED"] = "true" })
            .Build();
        var autonomy = new ConfiguredAutonomySettings(configuration, settings);

        Assert.That(autonomy.ServerEnabled, Is.True);
        autonomy.Disable();
        Assert.That(autonomy.ServerEnabled, Is.True);
    }

    [Test]
    public void ServerEnabled_readsStoredEnablementWhenTheEnvironmentIsUnset()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        settings.SetServerEnabled(true);

        Assert.That(new ConfiguredAutonomySettings(EmptyConfiguration(), settings).ServerEnabled, Is.True);
    }

    [Test]
    public void ServerEnabled_treatsOneAsAnEnvironmentOverride()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AGENTUP_AUTONOMY_ENABLED"] = "1" })
            .Build();

        Assert.That(new ConfiguredAutonomySettings(configuration, settings).ServerEnabled, Is.True);
    }

    [Test]
    public void ServerEnabled_ignoresAFalseEnvironmentValue()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        settings.SetServerEnabled(true);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AGENTUP_AUTONOMY_ENABLED"] = "false" })
            .Build();

        Assert.That(new ConfiguredAutonomySettings(configuration, settings).ServerEnabled, Is.True);
    }

    [Test]
    public void TryEnable_usesHostPresenceWhenTheWorkspaceFlagIsOff()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var autonomy = new ConfiguredAutonomySettings(EmptyConfiguration(), settings, new FixedHostPresence(true));

        Assert.That(autonomy.TryEnable(out var refusal), Is.True);
        Assert.That(refusal, Is.Null);
        Assert.That(settings.ServerEnabled, Is.True);
    }

    [Test]
    public void TryEnable_refusesWhenHostPresenceIsInjectedAndAbsent()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        settings.Save(JsonAgentWorkspaceSettingsRepository.Empty("ws") with { HostConnectionAuthorized = true });
        var autonomy = new ConfiguredAutonomySettings(EmptyConfiguration(), settings, new FixedHostPresence(false));

        Assert.That(autonomy.TryEnable(out var refusal), Is.False);
        Assert.That(refusal, Is.EqualTo(AutonomyPreconditions.Refusal(AutonomyPreconditions.HostConnectionAuthorized)));
    }

    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().AddInMemoryCollection().Build();

    private sealed class FixedHostPresence(bool present) : AgentUp.Server.Features.Agents.Interfaces.IHostConnectionPresence
    {
        public bool IsPresent() => present;
        public bool IsPresent(string workspaceId) => present;
    }
}

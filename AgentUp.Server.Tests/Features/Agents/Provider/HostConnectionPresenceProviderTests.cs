using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Features.Forge.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class HostConnectionPresenceProviderTests
{
    [Test]
    public void IsPresent_isFalseUntilAWorkspaceHostIsAuthorized()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        var host = new HostConnectionPresenceProvider(settings);

        Assert.That(host.IsPresent(), Is.False);
        Assert.That(host.IsPresent("ws"), Is.False);
    }

    [Test]
    public void IsPresent_readsTheWorkspaceAuthorizationFlag()
    {
        var settings = new InMemoryAgentWorkspaceSettingsRepository();
        settings.Save(JsonAgentWorkspaceSettingsRepository.Empty("ws") with { HostConnectionAuthorized = true });
        var host = new HostConnectionPresenceProvider(settings);

        Assert.Multiple(() =>
        {
            Assert.That(host.IsPresent(), Is.True);
            Assert.That(host.IsPresent("ws"), Is.True);
            Assert.That(host.IsPresent("other"), Is.False);
        });
    }

    [Test]
    public async Task IsPresent_readsAForgeCredentialWhenTheWorkspaceFlagIsOff()
    {
        var (forge, workspaceId) = await ForgeWithCredentialAsync();
        var host = new HostConnectionPresenceProvider(new InMemoryAgentWorkspaceSettingsRepository(), forge);

        Assert.Multiple(() =>
        {
            Assert.That(host.IsPresent(), Is.True);
            Assert.That(host.IsPresent(workspaceId), Is.True);
            Assert.That(host.IsPresent("other"), Is.False);
        });
    }

    [Test]
    public async Task IsPresent_staysFalseWhenTheForgeHasNoCredential()
    {
        var (forge, workspaceId) = await ForgeWithCredentialAsync(token: null);
        var host = new HostConnectionPresenceProvider(new InMemoryAgentWorkspaceSettingsRepository(), forge);

        Assert.That(host.IsPresent(), Is.False);
        Assert.That(host.IsPresent(workspaceId), Is.False);
    }

    private static async Task<(ForgeController Forge, string WorkspaceId)> ForgeWithCredentialAsync(string? token = "token")
    {
        var workspaces = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var workspace = await workspaces.RegisterAsync(ServerDomain.Workspace().Build());
        var credentials = new MemoryForgeCredentialStore();
        if (token is not null)
            credentials.Write(workspace.Id, "github", token);
        var service = new ForgeService(
            workspaces,
            new FakeEnabledCapabilityPackages().WithForges(new StubForgeCapability()),
            credentials,
            new FixedForgeGitRemoteResolver(new ForgeGitRemote("acme", "widgets", "main", "https://example.test/acme/widgets.git")),
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "http://localhost/callback")),
            new ForgeOAuthStateStore(),
            new FixedForgeOAuthExchange("exchanged"));
        return (new ForgeController(service), workspace.Id);
    }
}

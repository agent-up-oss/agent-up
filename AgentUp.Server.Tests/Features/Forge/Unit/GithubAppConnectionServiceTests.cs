using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.Forge.Providers;
using AgentUp.Server.Features.Forge.Services;

namespace AgentUp.Server.Tests.Features.Forge.Unit;

[TestFixture]
public sealed class GithubAppConnectionServiceTests
{
    [Test]
    public async Task Start_createsAWorkspaceSpecificManifestLaunch()
    {
        var store = new MemoryStore();
        var service = new GithubAppConnectionService(store, new GithubAppApiProvider(new OwnerHandler("User")));

        var launch = await service.StartAsync(
            "3b5c4f70-rest", "Workspace 1", "personal-owner", Host(), "http://localhost:5000", CancellationToken.None);
        var page = service.RegistrationPage(store.Value!.State);

        Assert.Multiple(() =>
        {
            Assert.That(launch.AuthorizationUrl, Does.StartWith("http://localhost:5000/api/forge/github/register?state="));
            Assert.That(store.Value.AppName, Is.EqualTo("Agent-Up-Workspace-1-3b5c4f70"));
            Assert.That(page, Does.Contain("https://github.test/settings/apps/new?state="));
            Assert.That(page, Does.Contain("pull_requests&quot;:&quot;write"));
            Assert.That(page, Does.Contain("install/callback?key="));
        });
    }

    [Test]
    public async Task Start_reusesTheRegisteredAppInsteadOfCreatingAnotherIdentity()
    {
        var store = new MemoryStore
        {
            Value = Connection() with { Slug = "agent-up-workspace-1", AppId = 42, PrivateKey = "key" }
        };
        var service = new GithubAppConnectionService(store, new GithubAppApiProvider());

        var launch = await service.StartAsync(
            "workspace-1", "Renamed", "owner", Host(), "http://elsewhere", CancellationToken.None);

        Assert.That(launch.AuthorizationUrl, Is.EqualTo("https://github.test/apps/agent-up-workspace-1/installations/new"));
        Assert.That(store.Writes, Is.EqualTo(0));
    }

    [Test]
    public async Task Start_createsAPrivateAppUnderTheRepositoryOrganization()
    {
        var store = new MemoryStore();
        var service = new GithubAppConnectionService(
            store,
            new GithubAppApiProvider(new OwnerHandler("Organization")));

        await service.StartAsync(
            "workspace-1", "Workspace 1", "acme", Host(), "http://localhost:5000", CancellationToken.None);
        var page = service.RegistrationPage(store.Value!.State);

        Assert.That(page, Does.Contain("https://github.test/organizations/acme/settings/apps/new?state="));
    }

    [Test]
    public void Disconnect_preservesTheAppRegistrationForReinstallation()
    {
        var store = new MemoryStore { Value = Connection() with { InstallationId = 99 } };
        var service = new GithubAppConnectionService(store, new GithubAppApiProvider());

        service.Disconnect("workspace-1");

        Assert.That(store.Value!.InstallationId, Is.Null);
        Assert.That(store.Value.AppId, Is.EqualTo(42));
    }

    private static ForgeHostConfiguration Host()
        => new("https://api.github.test/", "https://github.test/login/oauth/authorize", "");

    private static GithubAppConnection Connection()
        => new(
            "workspace-1", "state", "setup", "Agent-Up-Workspace-1-12345678",
            "http://localhost:5000", "https://api.github.test/", "https://github.test",
            42, "agent-up-workspace-1", "key");

    private sealed class MemoryStore : IGithubAppConnectionStore
    {
        public GithubAppConnection? Value { get; set; }
        public int Writes { get; private set; }
        public GithubAppConnection? Read(string workspaceId) => Value?.WorkspaceId == workspaceId ? Value : null;
        public GithubAppConnection? FindByState(string state) => Value?.State == state ? Value : null;
        public GithubAppConnection? FindBySetupKey(string setupKey) => Value?.SetupKey == setupKey ? Value : null;
        public void Write(GithubAppConnection connection) { Value = connection; Writes++; }
        public void Delete(string workspaceId) => Value = null;
    }

    private sealed class OwnerHandler(string type) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"type\":\"{type}\"}}")
            });
    }
}

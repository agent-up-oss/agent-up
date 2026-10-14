using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Server.Tests.Features.Forge.HTTP;

[TestFixture]
public sealed class ForgeHttpTests
{
    [Test]
    public async Task Get_returnsNotFoundForAnUnknownWorkspace()
    {
        using var host = await HostAsync();
        using var client = host.Factory.CreateClient();

        using var response = await client.GetAsync("/api/workspaces/missing/forge");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Get_returnsNoneWhenNoForgeModuleIsEnabled()
    {
        using var host = await HostAsync();
        using var client = host.Factory.CreateClient();
        var workspaceId = await RegisterAsync(client, host.Repository);

        var hosting = await client.GetFromJsonAsync<ForgeHostingDto>($"/api/workspaces/{workspaceId}/forge");

        Assert.That(hosting!.Availability, Is.EqualTo("none"));
        Assert.That(hosting.Proposal, Is.Null);
    }

    [Test]
    public async Task Login_returnsServiceUnavailableWhenOAuthIsNotConfigured()
    {
        using var host = await HostAsync();
        using var client = host.Factory.CreateClient();
        var workspaceId = await RegisterAsync(client, host.Repository);

        using var response = await client.PostAsync($"/api/workspaces/{workspaceId}/forge/login", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task Login_returnsNotFoundForAnUnknownWorkspace()
    {
        using var host = await HostAsync();
        using var client = host.Factory.CreateClient();

        using var response = await client.PostAsync("/api/workspaces/missing/forge/login", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Logout_returnsNotFoundForAnUnknownWorkspace()
    {
        using var host = await HostAsync();
        using var client = host.Factory.CreateClient();

        using var response = await client.PostAsync("/api/workspaces/missing/forge/logout", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    private static async Task<string> RegisterAsync(HttpClient client, string repository)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/workspaces",
            ServerDomain.Workspace().Named("widgets").At(repository).AtCommit("abc123").Build());
        response.EnsureSuccessStatusCode();
        var document = await response.Content.ReadFromJsonAsync<JsonElement>();
        return document.GetProperty("id").GetString()!;
    }

    private static async Task<HostContext> HostAsync()
    {
        var dataDirectory = Path.Join(TestContext.CurrentContext.WorkDirectory, $"agent-up-forge-{Guid.NewGuid():N}");
        var repository = Path.Join(dataDirectory, "widgets");
        await TestGitRepository.InitializeAsync(repository);
        var root = new WebApplicationFactory<Program>();
        var factory = root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:DataDirectory", dataDirectory);
            builder.UseSetting("AGENTUP_AUTH_DISABLED", "true");
        });
        return new HostContext(root, factory, dataDirectory, repository);
    }

    private sealed class HostContext(
        WebApplicationFactory<Program> root,
        WebApplicationFactory<Program> factory,
        string dataDirectory,
        string repository) : IDisposable
    {
        public WebApplicationFactory<Program> Factory { get; } = factory;
        public string Repository { get; } = repository;

        public void Dispose()
        {
            root.Dispose();
            if (Directory.Exists(dataDirectory))
                Directory.Delete(dataDirectory, recursive: true);
        }
    }
}

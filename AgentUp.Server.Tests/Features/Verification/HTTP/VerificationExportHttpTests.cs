using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Server.Features.Verification.DTOs;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Server.Tests.Features.Verification.HTTP;

[TestFixture]
public sealed class VerificationExportHttpTests
{
    [Test]
    public async Task Export_returnsNotFoundForAnUnknownWorkspace()
    {
        using var host = await HostAsync();
        using var client = host.Factory.CreateClient();

        using var response = await client.GetAsync("/api/workspaces/missing/verification/export");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Export_returnsADigestAndNeverTheCoveredMap()
    {
        using var host = await HostAsync();
        using var client = host.Factory.CreateClient();
        var workspaceId = await RegisterAsync(client, host.Repository);

        var export = await client.GetFromJsonAsync<VerificationExportDto>(
            $"/api/workspaces/{workspaceId}/verification/export");

        Assert.Multiple(() =>
        {
            Assert.That(export, Is.Not.Null);
            Assert.That(export!.CoveredDigest, Does.StartWith("sha256:"));
            Assert.That(typeof(VerificationExportDto).GetProperties().Select(property => property.Name),
                Does.Not.Contain("Covered"));
        });
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
        var dataDirectory = Path.Join(TestContext.CurrentContext.WorkDirectory, $"agent-up-export-{Guid.NewGuid():N}");
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

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Diagnostics.DTOs;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Server.Tests.Features.Diagnostics.HTTP;

[TestFixture]
public sealed class WorkspaceReviewSnapshotHttpTests
{
    [Test]
    public async Task Get_ReturnsWorkspaceIdentityWhenVerificationCannotRun()
    {
        using var host = CreateHost();
        using var client = host.Factory.CreateClient();
        using var registrationResponse = await client.PostAsJsonAsync("/api/workspaces", ServerDomain.Workspace()
            .Named("Shop")
            .At("/repo")
            .AtCommit("abc")
            .WithApplication(new ApplicationDefinitionBuilder("web", "npm start").At(".").Build())
            .Build());
        registrationResponse.EnsureSuccessStatusCode();
        using var registration = await JsonDocument.ParseAsync(await registrationResponse.Content.ReadAsStreamAsync());
        var workspaceId = registration.RootElement.GetProperty("id").GetString()!;

        using var snapshotResponse = await client.GetAsync($"/api/workspaces/{workspaceId}/review-snapshot");
        var snapshotBody = await snapshotResponse.Content.ReadAsStringAsync();
        Assert.That(snapshotResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), snapshotBody);
        var snapshot = JsonSerializer.Deserialize<WorkspaceReviewSnapshotDto>(snapshotBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.Multiple(() =>
        {
            Assert.That(snapshot!.WorkspaceId, Is.EqualTo(workspaceId));
            Assert.That(snapshot.DisplayName, Is.EqualTo("Shop"));
            Assert.That(snapshot.Commit, Is.EqualTo("abc"));
            Assert.That(snapshot.Applications.Select(app => app.Name), Is.EqualTo(["web"]));
            Assert.That(snapshot.Verification, Is.Null);
        });
    }

    [Test]
    public async Task Get_OmitsVerificationWhenTheWorktreeCannotExport()
    {
        var worktree = Path.Join(Path.GetTempPath(), $"agent-up-review-snapshot-worktree-{Guid.NewGuid():N}");
        Directory.CreateDirectory(worktree);
        File.WriteAllText(Path.Join(worktree, "agent-up.json"), "{ this is not json");
        try
        {
            using var host = CreateHost();
            using var client = host.Factory.CreateClient();
            using var registrationResponse = await client.PostAsJsonAsync("/api/workspaces", ServerDomain.Workspace()
                .Named("Shop")
                .At(worktree)
                .AtCommit("abc")
                .Build());
            registrationResponse.EnsureSuccessStatusCode();
            using var registration = await JsonDocument.ParseAsync(await registrationResponse.Content.ReadAsStreamAsync());
            var workspaceId = registration.RootElement.GetProperty("id").GetString()!;

            using var snapshotResponse = await client.GetAsync($"/api/workspaces/{workspaceId}/review-snapshot");
            var snapshotBody = await snapshotResponse.Content.ReadAsStringAsync();
            Assert.That(snapshotResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), snapshotBody);
            var snapshot = JsonSerializer.Deserialize<WorkspaceReviewSnapshotDto>(snapshotBody, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Assert.That(snapshot!.Verification, Is.Null);
        }
        finally
        {
            Directory.Delete(worktree, recursive: true);
        }
    }

    [Test]
    public async Task Get_ReturnsNotFoundForUnknownWorkspace()
    {
        using var host = CreateHost();
        using var client = host.Factory.CreateClient();
        using var response = await client.GetAsync("/api/workspaces/missing/review-snapshot");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Get_IncludesVerificationWhenTheWorktreeCanExport()
    {
        using var host = await CreateHostWithRepositoryAsync();
        using var client = host.Factory.CreateClient();
        using var registrationResponse = await client.PostAsJsonAsync("/api/workspaces", ServerDomain.Workspace()
            .Named("Shop")
            .At(host.Repository)
            .AtCommit("abc")
            .Build());
        registrationResponse.EnsureSuccessStatusCode();
        using var registration = await JsonDocument.ParseAsync(await registrationResponse.Content.ReadAsStreamAsync());
        var workspaceId = registration.RootElement.GetProperty("id").GetString()!;

        using var snapshotResponse = await client.GetAsync($"/api/workspaces/{workspaceId}/review-snapshot");
        var snapshotBody = await snapshotResponse.Content.ReadAsStringAsync();
        Assert.That(snapshotResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), snapshotBody);
        var snapshot = JsonSerializer.Deserialize<WorkspaceReviewSnapshotDto>(snapshotBody, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.That(snapshot!.Verification, Is.Not.Null);
        Assert.That(snapshot.Verification!.CoveredDigest, Does.StartWith("sha256:"));
    }

    private static HostContext CreateHost()
    {
        var dataDirectory = Path.Join(Path.GetTempPath(), $"agent-up-review-snapshot-http-{Guid.NewGuid():N}");
        var root = new WebApplicationFactory<Program>();
        var factory = root.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Storage:DataDirectory", dataDirectory);
            builder.UseSetting("AGENTUP_AUTH_DISABLED", "true");
        });
        return new HostContext(root, factory, dataDirectory);
    }

    private static async Task<HostContext> CreateHostWithRepositoryAsync()
    {
        var dataDirectory = Path.Join(Path.GetTempPath(), $"agent-up-review-snapshot-export-{Guid.NewGuid():N}");
        var repository = Path.Join(dataDirectory, "shop");
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
        string? repository = null) : IDisposable
    {
        public WebApplicationFactory<Program> Factory { get; } = factory;
        public string Repository { get; } = repository ?? dataDirectory;

        public void Dispose()
        {
            root.Dispose();
            if (Directory.Exists(dataDirectory))
                Directory.Delete(dataDirectory, recursive: true);
        }
    }
}

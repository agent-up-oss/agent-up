using System.Net;
using System.Net.Http.Json;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Verification.DTOs;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentUp.Server.Tests.Features.Diagnostics.HTTP;

[TestFixture]
public sealed class WorkspaceReviewSnapshotHttpTests
{
    [Test]
    public async Task Get_ReturnsHtmlWorkspaceIdentityWhenVerificationCannotRun()
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
        using var registration = await System.Text.Json.JsonDocument.ParseAsync(await registrationResponse.Content.ReadAsStreamAsync());
        var workspaceId = registration.RootElement.GetProperty("id").GetString()!;

        using var snapshotResponse = await client.GetAsync($"/api/workspaces/{workspaceId}/review-snapshot");
        var html = await snapshotResponse.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(snapshotResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), html);
            Assert.That(snapshotResponse.Content.Headers.ContentType?.MediaType, Is.EqualTo("text/html"));
            Assert.That(snapshotResponse.Content.Headers.ContentDisposition?.FileName, Is.EqualTo("agent-up-report.html"));
            Assert.That(html, Does.Contain("Shop"));
            Assert.That(html, Does.Contain("abc"));
            Assert.That(html, Does.Contain("web"));
            Assert.That(html, Does.Contain("<h2>Screenshots</h2>"));
            Assert.That(html, Does.Contain("<h2>Verification</h2>"));
            Assert.That(html, Does.Contain("Verification could not run"));
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
            using var registration = await System.Text.Json.JsonDocument.ParseAsync(await registrationResponse.Content.ReadAsStreamAsync());
            var workspaceId = registration.RootElement.GetProperty("id").GetString()!;

            using var snapshotResponse = await client.GetAsync($"/api/workspaces/{workspaceId}/review-snapshot");
            var html = await snapshotResponse.Content.ReadAsStringAsync();

            Assert.That(snapshotResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), html);
            Assert.That(html, Does.Contain("Verification could not run"));
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
    public async Task Get_IncludesScreenshotArtifactIdsFromAudit()
    {
        using var host = CreateHost();
        using var client = host.Factory.CreateClient();
        using var registrationResponse = await client.PostAsJsonAsync("/api/workspaces", ServerDomain.Workspace()
            .Named("Shop")
            .At("/repo")
            .AtCommit("abc")
            .Build());
        registrationResponse.EnsureSuccessStatusCode();
        using var registration = await System.Text.Json.JsonDocument.ParseAsync(await registrationResponse.Content.ReadAsStreamAsync());
        var workspaceId = registration.RootElement.GetProperty("id").GetString()!;
        using var recordResponse = await client.PostAsJsonAsync("/api/audit/record", ServerDomain.AuditRecord()
            .OfKind("browser")
            .From("mcp")
            .Doing("browser_screenshot")
            .Outcome("success")
            .ForWorkspace(workspaceId)
            .WithArtifacts("shot1")
            .WithDetail("url", "http://127.0.0.1:4010/")
            .WithDetail("mimeType", "image/png")
            .Build());
        recordResponse.EnsureSuccessStatusCode();

        using var snapshotResponse = await client.GetAsync($"/api/workspaces/{workspaceId}/review-snapshot");
        var html = await snapshotResponse.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(snapshotResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(html, Does.Contain("shot1"));
            Assert.That(html, Does.Contain("http://127.0.0.1:4010/"));
        });
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
        using var registration = await System.Text.Json.JsonDocument.ParseAsync(await registrationResponse.Content.ReadAsStreamAsync());
        var workspaceId = registration.RootElement.GetProperty("id").GetString()!;

        using var snapshotResponse = await client.GetAsync($"/api/workspaces/{workspaceId}/review-snapshot");
        var html = await snapshotResponse.Content.ReadAsStringAsync();
        Assert.That(snapshotResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK), html);

        var export = await client.GetFromJsonAsync<VerificationExportDto>(
            $"/api/workspaces/{workspaceId}/verification/export");

        Assert.Multiple(() =>
        {
            Assert.That(export, Is.Not.Null);
            Assert.That(html, Does.Contain(export!.CoveredDigest));
            Assert.That(html, Does.Contain(export.Revision));
            Assert.That(export.Checks.Select(check => check.CheckId).All(html.Contains), Is.True);
        });
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

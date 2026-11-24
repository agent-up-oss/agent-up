using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.TaskSource.Controllers;
using AgentUp.Server.Features.TaskSource.DTOs;
using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.TaskSource.Models;
using AgentUp.Server.Features.TaskSource.Providers;
using AgentUp.Server.Features.TaskSource.Services;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUp.Server.Tests.Features.TaskSource.HTTP;

[TestFixture]
public sealed class TaskSourceHttpTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task Get_listsBothHostsAndDoesNotIncludeAToken()
    {
        await using var host = await HostAsync();

        var body = await host.Client.GetStringAsync("/api/task-source");
        var source = JsonSerializer.Deserialize<TaskSourceDto>(body, Json);

        Assert.Multiple(() =>
        {
            Assert.That(source!.Modules.Select(module => module.ModuleId), Is.EqualTo(new[] { "github", "forgejo" }));
            Assert.That(body, Does.Not.Contain("secret-token"));
        });
    }

    [Test]
    public async Task Put_storesATokenThatCanReadPullRequestsAndDoesNotEchoIt()
    {
        await using var host = await HostAsync();

        var response = await host.Client.PutAsJsonAsync("/api/task-source", new SaveTaskSourceTokenRequest("github", "https://github.com", "secret-token"), Json);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(body, Does.Not.Contain("secret-token"));
            Assert.That(host.Credentials.Read("github")?.Token, Is.EqualTo("secret-token"));
            Assert.That(host.Credentials.Read("github")?.EndpointUrl, Is.EqualTo("https://github.com"));
            Assert.That(body, Does.Contain("\"endpointUrl\":\"https://github.com\""));
        });
    }

    [Test]
    public async Task Put_refusesATokenWithoutAHostUrl()
    {
        await using var host = await HostAsync();

        var response = await host.Client.PutAsJsonAsync("/api/task-source", new SaveTaskSourceTokenRequest("github", " ", "secret-token"), Json);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(body, Does.Contain("host URL"));
            Assert.That(host.Credentials.Read("github"), Is.Null);
        });
    }

    [Test]
    public async Task Put_refusesATokenThatCannotReadPullRequests()
    {
        await using var host = await HostAsync(new ForgeTokenAccess(true, false, "The token cannot read pull requests."));

        var response = await host.Client.PutAsJsonAsync("/api/task-source", new SaveTaskSourceTokenRequest("github", "https://github.com", "narrow"), Json);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(body, Does.Contain("pull requests"));
            Assert.That(host.Credentials.Read("github"), Is.Null);
        });
    }

    [Test]
    public async Task Delete_forgetsOneHost()
    {
        await using var host = await HostAsync();
        host.Credentials.Write("github", new TaskSourceCredential("github-token", "https://github.com"));
        host.Credentials.Write("forgejo", new TaskSourceCredential("forgejo-token", "https://git.example.test"));

        var response = await host.Client.DeleteAsync("/api/task-source/github");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(host.Credentials.Read("github"), Is.Null);
            Assert.That(host.Credentials.Read("forgejo")?.Token, Is.EqualTo("forgejo-token"));
        });
    }

    [Test]
    public async Task Get_repositoriesAndIssuesAndPullsUseTheStoredToken()
    {
        await using var host = await HostAsync();
        host.Credentials.Write("github", new TaskSourceCredential("token", "https://github.com"));

        var repositories = await host.Client.GetFromJsonAsync<TaskSourceRepositoryDto[]>("/api/task-source/repositories", Json);
        var issues = await host.Client.GetFromJsonAsync<JsonElement>(
            "/api/task-source/issues?moduleId=github&owner=acme&repository=widgets", Json);
        var pulls = await host.Client.GetFromJsonAsync<TaskSourcePullDto[]>(
            "/api/task-source/pulls?moduleId=github&owner=acme&repository=widgets", Json);

        Assert.Multiple(() =>
        {
            Assert.That(repositories!.Single().Name, Is.EqualTo("widgets"));
            Assert.That(issues.GetArrayLength(), Is.EqualTo(1));
            Assert.That(pulls!.Single().Reviews.Single().Standing, Is.EqualTo("reviewing"));
        });
    }

    private static async Task<Host> HostAsync(ForgeTokenAccess? access = null)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = ["--urls=http://127.0.0.1:0"] });
        builder.Services.AddControllers().AddApplicationPart(typeof(TaskSourceHttpController).Assembly);

        var directory = Path.Join(Path.GetTempPath(), $"task-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var credentials = new JsonTaskSourceCredentialStore(directory);
        var forge = new StubForgeCapability
        {
            TokenAccess = access ?? new ForgeTokenAccess(true, true, null),
            Repositories = [new ForgeRepository("acme", "widgets")],
            Issues = [new ForgeIssue("I-7", 7, "Cart badge", "", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "https://example.test/7")],
            PullRequests = [new ForgePullRequest("42", 42, "Product cards", "https://example.test/42", ["hector"], [])],
        };
        builder.Services.AddSingleton<IEnabledCapabilityPackages>(new FakeEnabledCapabilityPackages().WithForges(forge));
        builder.Services.AddSingleton<ITaskSourceCredentialStore>(credentials);
        builder.Services.AddSingleton<ITaskSourceTeam>(new FixedTeam());
        builder.Services.AddSingleton<TaskSourceService>();
        builder.Services.AddSingleton<TaskSourceController>();

        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();
        return new Host(app, new HttpClient { BaseAddress = new Uri(app.Urls.First()) }, directory, credentials);
    }

    private sealed class FixedTeam : ITaskSourceTeam
    {
        public Task<TaskSourceTeam> ForAsync(string owner, string repository, CancellationToken cancellationToken)
            => Task.FromResult(new TaskSourceTeam(new Dictionary<string, string> { ["hector"] = "devReviewer" }, ["devReviewer"]));
    }

    private sealed record Host(WebApplication App, HttpClient Client, string Directory, JsonTaskSourceCredentialStore Credentials) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
            if (System.IO.Directory.Exists(Directory))
                System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}

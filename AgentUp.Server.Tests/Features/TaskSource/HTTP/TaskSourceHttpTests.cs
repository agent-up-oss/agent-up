using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.Capabilities.Interfaces;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.TaskSource.Controllers;
using AgentUp.Server.Features.TaskSource.DTOs;
using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.TaskSource.Providers;
using AgentUp.Server.Features.TaskSource.Services;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace AgentUp.Server.Tests.Features.TaskSource.HTTP;

[TestFixture]
public sealed class TaskSourceHttpTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Test]
    public async Task GetSource_describesAServerNobodyHasPointedAnywhere()
    {
        await using var host = await HostAsync();

        var source = await host.Client.GetFromJsonAsync<TaskSourceDto>("/api/task-source", Json);

        Assert.Multiple(() =>
        {
            Assert.That(source!.Configured, Is.False);
            Assert.That(source.Modules.Single().ModuleId, Is.EqualTo("github"));
        });
    }

    [Test]
    public async Task PutSource_namesTheRepositoryTheAgentsWorkFrom()
    {
        await using var host = await HostAsync();

        var response = await host.Client.PutAsJsonAsync(
            "/api/task-source",
            new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"),
            Json);
        var source = await response.Content.ReadFromJsonAsync<TaskSourceDto>(Json);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(source!.Configured, Is.True);
            Assert.That(source.Owner, Is.EqualTo("agent-up-oss"));
            Assert.That(source.Connected, Is.False);
        });
    }

    [Test]
    public async Task PostLogin_answersWithSomewhereToSendTheReader()
    {
        await using var host = await HostAsync();
        await host.Client.PutAsJsonAsync(
            "/api/task-source",
            new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"),
            Json);

        var response = await host.Client.PostAsync("/api/task-source/login", null);
        var login = await response.Content.ReadFromJsonAsync<TaskSourceLoginDto>(Json);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(login!.AuthorizeUrl, Does.Contain("client_id=client"));
        });
    }

    [Test]
    public async Task PostLogin_beforeARepositoryIsNamedIsRefused()
    {
        await using var host = await HostAsync();

        var response = await host.Client.PostAsync("/api/task-source/login", null);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task TheCallbackConnectsTheSourceAndThenServesItsIssues()
    {
        var issue = new ForgeIssue("7", 3, "Restock", "body", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "u");
        await using var host = await HostAsync(issue);
        await host.Client.PutAsJsonAsync(
            "/api/task-source",
            new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"),
            Json);
        var login = await (await host.Client.PostAsync("/api/task-source/login", null))
            .Content.ReadFromJsonAsync<TaskSourceLoginDto>(Json);
        var state = System.Web.HttpUtility.ParseQueryString(new Uri(login!.AuthorizeUrl).Query)["state"];

        var callback = await host.Client.GetAsync($"/api/task-source/callback?code=abc&state={state}");
        var issues = await host.Client.GetFromJsonAsync<List<ForgeIssueDto>>("/api/task-source/issues", Json);

        Assert.Multiple(() =>
        {
            Assert.That(callback.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(issues!.Single().Number, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task TheCallbackRefusesAStateNobodyIssued()
    {
        await using var host = await HostAsync();

        var callback = await host.Client.GetAsync("/api/task-source/callback?code=abc&state=invented");

        Assert.That(callback.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task DeleteSource_forgetsTheRepository()
    {
        await using var host = await HostAsync();
        await host.Client.PutAsJsonAsync(
            "/api/task-source",
            new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"),
            Json);

        var response = await host.Client.DeleteAsync("/api/task-source");
        var source = await response.Content.ReadFromJsonAsync<TaskSourceDto>(Json);

        Assert.That(source!.Configured, Is.False);
    }

    private static async Task<Host> HostAsync(params ForgeIssue[] issues)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = ["--urls=http://127.0.0.1:0"] });
        builder.Services.AddControllers().AddApplicationPart(typeof(TaskSourceHttpController).Assembly);

        var directory = Path.Join(Path.GetTempPath(), $"task-source-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        builder.Services.AddSingleton<IEnabledCapabilityPackages>(
            new FakeEnabledCapabilityPackages().WithForges(new StubForgeCapability { Issues = issues }));
        builder.Services.AddSingleton<IForgeOAuthSettings>(
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "https://example.test/callback")));
        builder.Services.AddSingleton<IForgeOAuthExchange>(new FixedForgeOAuthExchange("exchanged"));
        builder.Services.AddSingleton<ITaskSourceStore>(_ => new JsonTaskSourceStore(directory));
        builder.Services.AddSingleton<ITaskSourceCredentialStore>(_ => new JsonTaskSourceCredentialStore(directory));
        builder.Services.AddSingleton<ITaskSourceStateStore, TaskSourceStateStore>();
        builder.Services.AddSingleton<TaskSourceService>();
        builder.Services.AddSingleton<TaskSourceController>();

        var app = builder.Build();
        app.MapControllers();
        await app.StartAsync();

        return new Host(app, new HttpClient { BaseAddress = new Uri(app.Urls.First()) }, directory);
    }

    private sealed record Host(WebApplication App, HttpClient Client, string Directory) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
            if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true);
        }
    }
}

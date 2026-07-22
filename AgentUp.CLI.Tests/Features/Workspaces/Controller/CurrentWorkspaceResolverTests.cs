using System.Net;
using System.Text.Json;
using AgentUp.CLI.Features.Workspaces.Controllers;
using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Providers;
using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Tests.Features.Workspaces.Controller;

[TestFixture]
public class CurrentWorkspaceResolverTests
{
    [Test]
    public async Task ResolveAsync_findsWorkspaceForCurrentDirectory()
    {
        var workspace = new WorkspaceDto("w1", "App", "/repo", "/repo/worktree", "main", "abc", "Running");
        var client = ClientReturning([workspace]);

        var result = await new CurrentWorkspaceResolver(client, "/repo/worktree")
            .ResolveAsync("query failed", "missing");

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Workspace!.Id, Is.EqualTo(workspace.Id));
        Assert.That(result.Workspace.WorktreePath, Is.EqualTo(workspace.WorktreePath));
    }

    [Test]
    public async Task ResolveAsync_returnsConfiguredMissingMessage()
    {
        var client = ClientReturning([]);

        var result = await new CurrentWorkspaceResolver(client, "/repo/worktree")
            .ResolveAsync("query failed", "missing workspace");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.EqualTo("missing workspace"));
    }

    [Test]
    public async Task ResolveAsync_returnsConfiguredQueryFailurePrefix()
    {
        var client = ClientThrowing(new InvalidOperationException("server unavailable"));

        var result = await new CurrentWorkspaceResolver(client, "/repo/worktree")
            .ResolveAsync("query failed", "missing workspace");

        Assert.That(result.Succeeded, Is.False);
        Assert.That(result.Error, Is.EqualTo("query failed: server unavailable"));
    }

    private static WorkspaceApiClient ClientReturning(IReadOnlyList<WorkspaceDto> workspaces)
    {
        var json = JsonSerializer.Serialize(workspaces);
        return new WorkspaceApiClient(new HttpClient(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json)
        }))
        {
            BaseAddress = new Uri("http://localhost")
        });
    }

    private static WorkspaceApiClient ClientThrowing(Exception exception)
        => new(new HttpClient(new StubHandler(_ => throw exception)) { BaseAddress = new Uri("http://localhost") });

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _response;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response)
        {
            _response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_response(request));
    }
}

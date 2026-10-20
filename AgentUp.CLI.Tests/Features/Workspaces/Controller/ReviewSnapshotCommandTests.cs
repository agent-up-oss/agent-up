using System.Net;
using System.Text.Json;
using AgentUp.CLI.Features.Workspaces.Controllers;
using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Interfaces;
using AgentUp.CLI.Features.Workspaces.Models;
using AgentUp.CLI.Features.Workspaces.Providers;
using AgentUp.CLI.Features.Workspaces.Services;
using AgentUp.CLI.Tests.Support;

namespace AgentUp.CLI.Tests.Features.Workspaces.Controller;

[TestFixture]
public sealed class ReviewSnapshotCommandTests
{
    [Test]
    public async Task RunAsync_WritesJsonForResolvedWorkspace()
    {
        var workspaceRoot = Path.Join(Path.GetTempPath(), "AgentUp-ReviewSnapshotCommandTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(workspaceRoot);
        File.WriteAllText(Path.Join(workspaceRoot, "agent-up.json"), "{}");
        try
        {
            using var writer = new StringWriter();
            var output = new WorkspaceCommandOutputService(writer);
            const string snapshot = """{"workspaceId":"w1","displayName":"Shop"}""";
            var client = ClientReturning(
                [CliDomain.Workspace()
                    .WithId("w1")
                    .Named("Shop")
                    .WithRepositoryPath(workspaceRoot)
                    .WithWorktreePath(workspaceRoot)
                    .OnBranch("main")
                    .AtCommit("abc")
                    .InState("Running")
                    .Build()],
                snapshot);
            var command = new ReviewSnapshotCommand(CreateService(client, workspaceRoot), output);

            var exitCode = await command.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.Zero);
                Assert.That(writer.ToString(), Does.Contain("\"displayName\":\"Shop\""));
            });
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Test]
    public async Task RunAsync_WritesFailureWhenWorkspaceMissing()
    {
        var workspaceRoot = Path.Join(Path.GetTempPath(), "AgentUp-ReviewSnapshotCommandMissing", Guid.NewGuid().ToString());
        Directory.CreateDirectory(workspaceRoot);
        File.WriteAllText(Path.Join(workspaceRoot, "agent-up.json"), "{}");
        try
        {
            using var writer = new StringWriter();
            var output = new WorkspaceCommandOutputService(writer);
            var command = new ReviewSnapshotCommand(
                CreateService(ClientReturning([], null), workspaceRoot),
                output);

            var exitCode = await command.RunAsync();

            Assert.Multiple(() =>
            {
                Assert.That(exitCode, Is.EqualTo(1));
                Assert.That(writer.ToString(), Does.Contain("No workspace found"));
            });
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    [Test]
    public async Task Help_ListsReviewSnapshot()
    {
        using var writer = new StringWriter();
        var controller = AgentUp.CLI.Composition.CliRunnerFactory.Create("http://127.0.0.1:9", ".", writer);

        var exitCode = await controller.RunAsync([]);

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(writer.ToString(), Does.Contain("review-snapshot"));
        });
    }

    private static WorkspaceCommandService CreateService(WorkspaceApiClient client, string workspaceRoot)
        => new(
            client,
            new FakeConfigurationProvider(),
            new FakeIdentityProvider(),
            new CurrentWorkspaceResolver(client, workspaceRoot),
            workspaceRoot);

    private static WorkspaceApiClient ClientReturning(
        IReadOnlyList<WorkspaceDto> workspaces,
        string? snapshot)
        => new(new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/review-snapshot", StringComparison.Ordinal))
            {
                return snapshot is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(snapshot)
                    };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(workspaces))
            };
        }))
        {
            BaseAddress = new Uri("http://localhost")
        });

    private sealed class FakeConfigurationProvider : IWorkspaceConfigurationProvider
    {
        public Task<WorkspaceConfigurationResult> LoadAsync(string workingDirectory)
            => Task.FromResult(new WorkspaceConfigurationResult(null, null, "unused"));
    }

    private sealed class FakeIdentityProvider : IWorkspaceIdentityProvider
    {
        public Task<WorkspaceIdentity> ReadAsync(string workingDirectory)
            => Task.FromResult(new WorkspaceIdentity("/repo", "main", "abc"));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(response(request));
    }
}

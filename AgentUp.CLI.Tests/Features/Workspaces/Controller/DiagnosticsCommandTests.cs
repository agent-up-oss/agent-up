using System.Net;
using System.Text.Json;
using AgentUp.CLI.Features.Workspaces.Controllers;
using AgentUp.CLI.Features.Workspaces.DTOs;
using AgentUp.CLI.Features.Workspaces.Interfaces;
using AgentUp.CLI.Features.Workspaces.Models;
using AgentUp.CLI.Features.Workspaces.Providers;
using AgentUp.CLI.Features.Workspaces.Services;

namespace AgentUp.CLI.Tests.Features.Workspaces.Controller;

[TestFixture]
public sealed class DiagnosticsCommandTests
{
    private string _workspaceRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _workspaceRoot = Path.Join(Path.GetTempPath(), "AgentUp-DiagnosticsCommandTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_workspaceRoot);
        File.WriteAllText(Path.Join(_workspaceRoot, "agent-up.json"), "{}");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_workspaceRoot, recursive: true);

    [Test]
    public async Task RunAsync_WritesDiagnosticsForResolvedWorkspace()
    {
        using var writer = new StringWriter();
        var output = new WorkspaceCommandOutputService(writer);
        var diagnostics = new WorkspaceDiagnosticsDto(
            "w1", "Shop", "Running", "Healthy", DateTimeOffset.UtcNow,
            [new ApplicationDiagnosticsDto("web", "Healthy", "Healthy", ["ready"], false)],
            []);
        var client = ClientReturning(
            [new WorkspaceDto("w1", "Shop", _workspaceRoot, _workspaceRoot, "main", "abc", "Running")],
            diagnostics);
        var service = CreateService(client);
        var command = new DiagnosticsCommand(service, output);

        var exitCode = await command.RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(writer.ToString(), Does.Contain("Diagnostics: Shop"));
        });
    }

    [Test]
    public async Task RunAsync_WritesFailureWhenWorkspaceMissing()
    {
        using var writer = new StringWriter();
        var output = new WorkspaceCommandOutputService(writer);
        var client = ClientReturning([], null);
        var service = CreateService(client);
        var command = new DiagnosticsCommand(service, output);

        var exitCode = await command.RunAsync();

        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.EqualTo(1));
            Assert.That(writer.ToString(), Does.Contain("No workspace found"));
        });
    }

    private WorkspaceCommandService CreateService(WorkspaceApiClient client)
        => new(
            client,
            new FakeConfigurationProvider(),
            new FakeIdentityProvider(),
            new CurrentWorkspaceResolver(client, _workspaceRoot),
            _workspaceRoot);

    private static WorkspaceApiClient ClientReturning(
        IReadOnlyList<WorkspaceDto> workspaces,
        WorkspaceDiagnosticsDto? diagnostics)
        => new(new HttpClient(new StubHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath.Contains("/diagnostics/", StringComparison.Ordinal))
            {
                return diagnostics is null
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(JsonSerializer.Serialize(diagnostics))
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

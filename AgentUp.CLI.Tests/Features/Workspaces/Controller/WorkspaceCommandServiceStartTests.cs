using System.Net;
using System.Text.Json;
using AgentUp.CLI.Features.Workspaces.Interfaces;
using AgentUp.CLI.Features.Workspaces.Models;
using AgentUp.CLI.Features.Workspaces.Providers;
using AgentUp.CLI.Features.Workspaces.Services;
using AgentUp.CLI.Tests.Support;

namespace AgentUp.CLI.Tests.Features.Workspaces.Controller;

[TestFixture]
public sealed class WorkspaceCommandServiceStartTests
{
    [Test]
    public async Task StartAsync_postsRuntimeSectionsAndLegacyCollections()
    {
        var workspaceRoot = Directory.CreateTempSubdirectory("AgentUp-StartServiceTests").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Join(workspaceRoot, "agent-up.json"),
                """
                {
                  "name": "App",
                  "applications": [{ "name": "web", "command": "npm start" }],
                  "desktopApplications": [{ "name": "editor", "command": "code ." }],
                  "services": [{ "name": "db", "image": "postgres:16" }],
                  "python": [{ "name": "api", "script": "main.py" }]
                }
                """);

            string? posted = null;
            var workspace = CliDomain.Workspace().WithId("w1").Named("App").At(workspaceRoot).Build();
            var client = new WorkspaceApiClient(new HttpClient(new StubHandler(async request =>
            {
                if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/workspaces")
                {
                    posted = await request.Content!.ReadAsStringAsync();
                    return JsonResponse(workspace);
                }

                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }))
            {
                BaseAddress = new Uri("http://localhost")
            });

            var result = await new WorkspaceCommandService(
                client,
                new WorkspaceConfigurationProvider(),
                new StubIdentity(),
                new CurrentWorkspaceResolver(client, workspaceRoot),
                workspaceRoot).StartAsync();

            using var document = JsonDocument.Parse(posted!);
            var root = document.RootElement;
            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True);
                Assert.That(root.GetProperty("applications")[0].GetProperty("name").GetString(), Is.EqualTo("web"));
                Assert.That(root.GetProperty("desktopApplications")[0].GetProperty("name").GetString(), Is.EqualTo("editor"));
                Assert.That(root.GetProperty("services")[0].GetProperty("name").GetString(), Is.EqualTo("db"));
                Assert.That(root.GetProperty("runtimeSections")[0].GetProperty("moduleId").GetString(), Is.EqualTo("python"));
                Assert.That(root.TryGetProperty("dotnet", out _), Is.False);
                Assert.That(root.TryGetProperty("docker", out _), Is.False);
            });
        }
        finally
        {
            Directory.Delete(workspaceRoot, recursive: true);
        }
    }

    private static HttpResponseMessage JsonResponse(object payload)
        => new(HttpStatusCode.Created)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json")
        };

    private sealed class StubIdentity : IWorkspaceIdentityProvider
    {
        public Task<WorkspaceIdentity> ReadAsync(string workingDirectory)
            => Task.FromResult(new WorkspaceIdentity("/repo", "main", "abc"));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => response(request);
    }
}

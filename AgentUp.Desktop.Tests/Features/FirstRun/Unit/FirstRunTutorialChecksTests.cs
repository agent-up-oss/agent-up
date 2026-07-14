using AgentUp.Desktop.Features.Applications.Http;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.Ports.Http;
using AgentUp.Desktop.Features.Workspaces.Http;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.FirstRun.Unit;

[TestFixture]
public class FirstRunTutorialChecksTests
{
    private string _testRoot = "";

    [SetUp]
    public void SetUp()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), $"agent-up-js-sample-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testRoot))
            Directory.Delete(_testRoot, recursive: true);
    }

    [Test]
    public async Task CreateJavaScriptSampleAsync_writesReactAndExpressSampleFiles()
    {
        var checks = CreateChecks([]);

        var result = await checks.CreateJavaScriptSampleAsync(_testRoot);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(File.Exists(Path.Combine(_testRoot, "web", "package.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(_testRoot, "web", "index.html")), Is.True);
        Assert.That(File.Exists(Path.Combine(_testRoot, "web", "src-App.jsx")), Is.True);
        Assert.That(File.Exists(Path.Combine(_testRoot, "api", "package.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(_testRoot, "api", "server.js")), Is.True);
        Assert.That(File.Exists(Path.Combine(_testRoot, "docker-compose.yaml")), Is.True);
    }

    [Test]
    public async Task CheckJavaScriptProjectFilesAsync_succeeds_whenDockerComposeExists()
    {
        var checks = CreateChecks([]);
        await checks.CreateJavaScriptSampleAsync(_testRoot);

        var result = await checks.CheckJavaScriptProjectFilesAsync(_testRoot);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task CreateAgentUpJsonAsync_writesProjectConfiguration()
    {
        var checks = CreateChecks([]);
        await checks.CreateJavaScriptSampleAsync(_testRoot);

        var create = await checks.CreateAgentUpJsonAsync(_testRoot);
        var check = await checks.CheckAgentUpJsonAsync(_testRoot);

        Assert.That(create.IsSuccess, Is.True);
        Assert.That(check.IsSuccess, Is.True);
        Assert.That(File.ReadAllText(Path.Combine(_testRoot, "agent-up.json")), Does.Contain("docker compose up database -d"));
    }

    [Test]
    public async Task CheckJavaScriptWorkspaceAsync_succeeds_whenServerHasSampleWorkspace()
    {
        await CreateSampleWithAgentUpJsonAsync();
        var checks = CreateChecks([SampleWorkspace(_testRoot, 5100, 5101, 5102)]);

        var result = await checks.CheckJavaScriptWorkspaceAsync(_testRoot);

        Assert.That(result.IsSuccess, Is.True);
    }

    [Test]
    public async Task CheckDuplicatedJavaScriptWorkspacesAsync_fails_whenPortsCollide()
    {
        await CreateSampleWithAgentUpJsonAsync();
        var duplicate = Path.Combine(Path.GetTempPath(), $"agent-up-js-sample-copy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(duplicate);
        try
        {
            var checks = CreateChecks(
            [
                SampleWorkspace(_testRoot, 5100, 5101, 5102),
                SampleWorkspace(duplicate, 5100, 5101, 5102)
            ]);

            var result = await checks.CheckDuplicatedJavaScriptWorkspacesAsync(_testRoot);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Message, Does.Contain("collides"));
        }
        finally
        {
            if (Directory.Exists(duplicate))
                Directory.Delete(duplicate, recursive: true);
        }
    }

    [Test]
    public async Task CheckDuplicatedJavaScriptWorkspacesAsync_succeeds_whenTwoSampleWorkspacesHaveUniquePorts()
    {
        await CreateSampleWithAgentUpJsonAsync();
        var duplicate = Path.Combine(Path.GetTempPath(), $"agent-up-js-sample-copy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(duplicate);
        try
        {
            var checks = CreateChecks(
            [
                SampleWorkspace(_testRoot, 5100, 5101, 5102),
                SampleWorkspace(duplicate, 5200, 5201, 5202)
            ]);

            var result = await checks.CheckDuplicatedJavaScriptWorkspacesAsync(_testRoot);

            Assert.That(result.IsSuccess, Is.True);
        }
        finally
        {
            if (Directory.Exists(duplicate))
                Directory.Delete(duplicate, recursive: true);
        }
    }

    private async Task CreateSampleWithAgentUpJsonAsync()
    {
        var checks = CreateChecks([]);
        await checks.CreateJavaScriptSampleAsync(_testRoot);
        await checks.CreateAgentUpJsonAsync(_testRoot);
    }

    private static FirstRunTutorialChecks CreateChecks(List<WorkspaceDto> workspaces)
    {
        var handler = new FakeHttpMessageHandler(workspaces);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        return new FirstRunTutorialChecks(new WorkspaceApiClient(http));
    }

    private static WorkspaceDto SampleWorkspace(string path, int webPort, int apiPort, int postgresPort)
        => new("ws-" + Path.GetFileName(path), "Sample", path, path, "main", "abc123", "Running")
        {
            Applications =
            [
                new ApplicationDto("React SPA", "npm run dev", "web", "Running")
                {
                    AllocatedPorts = [new PortMappingDto("WEB_PORT", 5173, webPort)]
                },
                new ApplicationDto("Express API", "npm run dev", "api", "Running")
                {
                    AllocatedPorts = [new PortMappingDto("API_PORT", 3001, apiPort)]
                },
                new ApplicationDto("Postgres", "", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto("POSTGRES_PORT", 5432, postgresPort, "tcp")]
                }
            ]
        };
}

using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.FirstRun.DTOs;
using AgentUp.Desktop.Features.FirstRun.Interfaces;
using AgentUp.Desktop.Features.FirstRun.Services;
using AgentUp.Desktop.Features.Ports.DTOs;
using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Providers;
using AgentUp.Desktop.Features.Workspaces.Services;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.FirstRun.Provider;

[TestFixture]
public class FirstRunTutorialChecksTests
{
    private string _testRoot = "";

    [SetUp]
    public void SetUp()
    {
        _testRoot = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "agent-up-tutorial", "example-agent1");
        Directory.CreateDirectory(_testRoot);
    }

    [TearDown]
    public void TearDown()
    {
        var guidRoot = Directory.GetParent(_testRoot)?.Parent?.FullName;
        if (guidRoot is not null && Directory.Exists(guidRoot))
            Directory.Delete(guidRoot, recursive: true);
    }

    [Test]
    public async Task CheckDockerAsync_checksDockerCliThenEngineBeforeAgentUpCli()
    {
        var calls = new List<string>();
        var checks = CreateChecks([], (fileName, arguments, _, _, _) =>
        {
            calls.Add($"{fileName} {arguments}");
            var stdout = fileName == "docker" && arguments == "--version"
                ? "Docker version 27.0.0"
                : "";
            return Task.FromResult(new FirstRunProcessResult(0, stdout, ""));
        });

        var result = await checks.CheckDockerAsync();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Message, Does.Contain("Docker version 27.0.0"));
        Assert.That(calls, Is.EqualTo(new[]
        {
            "docker --version",
            "docker info",
            "agent-up --help"
        }));
    }

    [Test]
    public async Task CheckDockerAsync_failsBeforeAgentUpCli_whenDockerEngineIsDown()
    {
        var calls = new List<string>();
        var checks = CreateChecks([], (fileName, arguments, _, _, _) =>
        {
            calls.Add($"{fileName} {arguments}");
            return Task.FromResult(arguments == "info"
                ? new FirstRunProcessResult(1, "", "Cannot connect to the Docker daemon")
                : new FirstRunProcessResult(0, "Docker version 27.0.0", ""));
        });

        var result = await checks.CheckDockerAsync();

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.Message, Does.Contain("Cannot connect to the Docker daemon"));
        Assert.That(calls, Is.EqualTo(new[]
        {
            "docker --version",
            "docker info"
        }));
    }

    [Test]
    public async Task CreateJavaScriptSampleAsync_reportsTheDirectoryItWroteInto()
    {
        var checks = CreateChecks([]);

        var result = await checks.CreateJavaScriptSampleAsync(_testRoot);

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.ProjectDirectory, Is.EqualTo(_testRoot));
    }

    [Test]
    public async Task CreateJavaScriptSampleAsync_writesEveryFileTheSampleNeedsToRun()
    {
        var checks = CreateChecks([]);

        await checks.CreateJavaScriptSampleAsync(_testRoot);

        string[] expected =
        [
            Path.Join("web", "package.json"),
            Path.Join("web", "index.html"),
            Path.Join("web", "src-App.jsx"),
            Path.Join("web", "vite.config.mjs"),
            Path.Join("api", "package.json"),
            Path.Join("api", "server.js"),
            Path.Join("api", "products.json"),
            "docker-compose.yaml"
        ];

        var missing = expected.Where(file => !File.Exists(Path.Join(_testRoot, file))).ToArray();
        Assert.That(missing, Is.Empty);
    }

    // The sample is the first thing a new user runs, so an unpinned dependency turns a
    // tutorial into whatever npm published this morning.
    [TestCase("web", "package.json", "\"vite\": \"5.4.11\"")]
    [TestCase("api", "package.json", "\"express\": \"4.18.3\"")]
    [TestCase("api", "package.json", "\"pg\": \"8.12.0\"")]
    public async Task CreateJavaScriptSampleAsync_pinsSampleDependencies(
        string directory, string file, string expected)
    {
        var checks = CreateChecks([]);

        await checks.CreateJavaScriptSampleAsync(_testRoot);

        var contents = await File.ReadAllTextAsync(Path.Join(_testRoot, directory, file));
        Assert.That(contents, Does.Contain(expected));
        Assert.That(contents, Does.Not.Contain("\"latest\""));
    }

    [Test]
    public async Task CreateJavaScriptSampleAsync_writesAReactAppThatReadsTheProductApi()
    {
        var checks = CreateChecks([]);

        await checks.CreateJavaScriptSampleAsync(_testRoot);

        var appJsx = await File.ReadAllTextAsync(Path.Join(_testRoot, "web", "src-App.jsx"));
        Assert.That(appJsx, Does.Contain("Product Operations Dashboard"));
        Assert.That(appJsx, Does.Contain("/api/products"));
        Assert.That(appJsx, Does.Contain("Unit Price"));
    }

    // Both ports are allocated by the Server, so the sample has to take them from the
    // environment rather than hard-coding them.
    [Test]
    public async Task CreateJavaScriptSampleAsync_takesBothPortsFromTheEnvironment()
    {
        var checks = CreateChecks([]);

        await checks.CreateJavaScriptSampleAsync(_testRoot);

        var viteConfig = await File.ReadAllTextAsync(Path.Join(_testRoot, "web", "vite.config.mjs"));
        Assert.That(viteConfig, Does.Contain("process.env.WEB_PORT"));
        Assert.That(viteConfig, Does.Contain("process.env.API_PORT"));
        Assert.That(viteConfig, Does.Contain("__API_PORT__"));
    }

    [Test]
    public async Task CreateJavaScriptSampleAsync_writesAnApiThatSeedsAndQueriesPostgres()
    {
        var checks = CreateChecks([]);

        await checks.CreateJavaScriptSampleAsync(_testRoot);

        var serverJs = await File.ReadAllTextAsync(Path.Join(_testRoot, "api", "server.js"));
        Assert.That(serverJs, Does.Contain("create table if not exists products"));
        Assert.That(serverJs, Does.Contain(
            "select sku, name, category, status, region, inventory, unit_price, margin, updated_at"));
        Assert.That(serverJs, Does.Contain("app.get('/api/products'"));
        Assert.That(serverJs, Does.Contain("Seeded ${products.length} product row(s) into Postgres."));
    }

    [Test]
    public async Task CreateJavaScriptSampleAsync_writesAnApiThatDocumentsItself()
    {
        var checks = CreateChecks([]);

        await checks.CreateJavaScriptSampleAsync(_testRoot);

        var serverJs = await File.ReadAllTextAsync(Path.Join(_testRoot, "api", "server.js"));
        Assert.That(serverJs, Does.Contain("openapi: '3.0.3'"));
        Assert.That(serverJs, Does.Contain("Product API Explorer"));
        Assert.That(serverJs, Does.Contain("app.get('/openapi.json'"));
        Assert.That(serverJs, Does.Contain("app.get('/',"));
        Assert.That(serverJs, Does.Contain("Express API querying Postgres"));
    }

    [Test]
    public async Task CreateJavaScriptSampleAsync_seedsTheProductCatalogue()
    {
        var checks = CreateChecks([]);

        await checks.CreateJavaScriptSampleAsync(_testRoot);

        var products = await File.ReadAllTextAsync(Path.Join(_testRoot, "api", "products.json"));
        Assert.That(products, Does.Contain("Atlas Analytics Seat"));
    }

    [Test]
    public async Task CreateAgentUpJsonAsync_usesCleanPinnedNpmInstallCommand()
    {
        var checks = CreateChecks([]);
        await checks.CreateJavaScriptSampleAsync(_testRoot);

        var result = await checks.CreateAgentUpJsonAsync(_testRoot);

        Assert.That(result.IsSuccess, Is.True);
        var agentUpJson = File.ReadAllText(Path.Join(_testRoot, "agent-up.json"));
        Assert.That(agentUpJson, Does.Contain("rm -rf node_modules package-lock.json && npm install --package-lock=false && npm run dev"));
        Assert.That(agentUpJson, Does.Contain("docker compose up database -d && docker compose logs -f database"));
    }

    [Test]
    public async Task CreateJavaScriptSampleAsync_generatesExampleAgent1Path_whenNoPathExists()
    {
        var checks = CreateChecks([]);

        var result = await checks.CreateJavaScriptSampleAsync();

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.ProjectDirectory, Does.EndWith(Path.Join("agent-up-tutorial", "example-agent1")));
        Assert.That(File.Exists(Path.Join(result.ProjectDirectory!, "docker-compose.yaml")), Is.True);
        Directory.Delete(Directory.GetParent(result.ProjectDirectory!)!.Parent!.FullName, recursive: true);
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
        Assert.That(File.ReadAllText(Path.Join(_testRoot, "agent-up.json")), Does.Contain("docker compose logs -f database"));
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
        var duplicate = Path.Join(Path.GetTempPath(), $"agent-up-js-sample-copy-{Guid.NewGuid():N}");
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
        var duplicate = Path.Join(Path.GetTempPath(), $"agent-up-js-sample-copy-{Guid.NewGuid():N}");
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
        return new FirstRunTutorialChecks(
            new WorkspacesController(new WorkspaceListService(new WorkspaceApiClient(http))),
            new FakeFirstRunProcessProvider((_, _, _, _, _) => Task.FromResult(new FirstRunProcessResult(0, "", ""))));
    }

    private static FirstRunTutorialChecks CreateChecks(
        List<WorkspaceDto> workspaces,
        Func<string, string, TimeSpan, CancellationToken, string?, Task<FirstRunProcessResult>> processRunner)
    {
        var handler = new FakeHttpMessageHandler(workspaces);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:5000") };
        return new FirstRunTutorialChecks(
            new WorkspacesController(new WorkspaceListService(new WorkspaceApiClient(http))),
            new FakeFirstRunProcessProvider(processRunner));
    }

    private sealed class FakeFirstRunProcessProvider(
        Func<string, string, TimeSpan, CancellationToken, string?, Task<FirstRunProcessResult>> run) : IFirstRunProcessProvider
    {
        public Task<FirstRunProcessResult> RunAsync(
            string fileName,
            string arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken,
            string? workingDirectory = null)
            => run(fileName, arguments, timeout, cancellationToken, workingDirectory);
    }

    private static WorkspaceDto SampleWorkspace(string path, int webPort, int apiPort, int postgresPort)
        => DesktopDomain.Workspace()
            .WithId("ws-" + Path.GetFileName(path))
            .Named("Sample")
            .WithRepositoryPath(path)
            .WithWorktreePath(path)
            .OnBranch(DesktopDomain.ThirdBranch)
            .WithApplication(
                new ApplicationDtoBuilder("React SPA", "npm run dev")
                    .At("web")
                    .WithPort(DesktopDomain.Port().Named("WEB_PORT").Declaring(5173).AllocatedTo(webPort).Build())
                    .Build())
            .WithApplication(
                new ApplicationDtoBuilder("Express API", "npm run dev")
                    .At("api")
                    .WithPort(DesktopDomain.Port().Named("API_PORT").Declaring(3001).AllocatedTo(apiPort).Build())
                    .Build())
            .WithApplication(
                new ApplicationDtoBuilder("Postgres", "")
                    .WithPort(DesktopDomain.Port()
                        .Named("POSTGRES_PORT")
                        .Declaring(5432)
                        .AllocatedTo(postgresPort)
                        .WithProtocol("tcp")
                        .Build())
                    .Build())
            .Build();
}

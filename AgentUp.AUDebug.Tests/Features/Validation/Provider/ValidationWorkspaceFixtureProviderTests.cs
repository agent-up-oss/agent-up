using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using AgentUp.AUDebug.Features.Validation.Providers;
using AgentUp.AUDebug.Tests.Fake;

namespace AgentUp.AUDebug.Tests.Features.Validation.Provider;

[TestFixture]
public sealed class ValidationWorkspaceFixtureProviderTests
{
    [Test]
    public void CreateDataDirectory_createsAnIsolatedDataFolder()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-fixture", Guid.NewGuid().ToString("N"));
        var provider = new ValidationWorkspaceFixtureProvider(new FakePathValidator(root));

        var data = provider.CreateDataDirectory();

        Assert.That(Directory.Exists(data), Is.True);
        Assert.That(data, Does.Contain("agent-up-desktop-validate"));
        Assert.That(Path.GetFileName(data), Is.EqualTo("data"));
    }

    [Test]
    public async Task PrepareAsync_registersTheReferenceWorkspace()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-fixture", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(
            Path.Join(root, "agent-up.json"),
            """{"name":"Agent Up","applications":[{"name":"Docs","command":"npm run start"}]}""");
        var handler = new RecordingHandler();
        var provider = new ValidationWorkspaceFixtureProvider(new FakePathValidator(root), handler);

        await provider.PrepareAsync("http://127.0.0.1:5001", CancellationToken.None);

        Assert.That(handler.Method, Is.EqualTo("POST"));
        Assert.That(handler.Path, Does.Contain("/api/workspaces"));
        Assert.That(handler.Body, Does.Contain(ValidationWorkspaceFixtureProvider.WorkspaceDisplayName));
        Assert.That(handler.Body, Does.Contain("\"Docs\""));
        Assert.That(handler.Body, Does.Contain(root));
    }

    [Test]
    public void ServerEnvironment_writesAScriptedAgentShimOntoPath()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-fixture", Guid.NewGuid().ToString("N"));
        var agents = Path.Join(root, "AgentUp.TestAgents", "bin", "Debug", "net10.0");
        Directory.CreateDirectory(agents);
        var executable = Path.Join(agents, OperatingSystem.IsWindows() ? "AgentUp.TestAgents.exe" : "AgentUp.TestAgents");
        File.WriteAllText(executable, "#!/bin/sh\n");
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                executable,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        var provider = new ValidationWorkspaceFixtureProvider(new FakePathValidator(root));
        var data = provider.CreateDataDirectory();

        var environment = provider.ServerEnvironment(data);

        var shim = environment[ValidationWorkspaceFixtureProvider.ScriptedAgentCommandKey];
        Assert.That(File.Exists(shim), Is.True);
        Assert.That(File.ReadAllText(shim), Does.Contain(executable));
        Assert.That(environment["PATH"], Does.StartWith(Path.GetDirectoryName(shim)!));
    }

    [Test]
    public async Task PrepareAsync_registersIdentityWhenAgentUpJsonIsMissing()
    {
        var root = Path.Join(Path.GetTempPath(), "au-debug-fixture", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var handler = new RecordingHandler();
        var provider = new ValidationWorkspaceFixtureProvider(new FakePathValidator(root), handler);

        await provider.PrepareAsync("http://127.0.0.1:5001", CancellationToken.None);

        Assert.That(handler.Body, Does.Contain(ValidationWorkspaceFixtureProvider.WorkspaceDisplayName));
        Assert.That(handler.Body, Does.Not.Contain("applications"));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public string? Method { get; private set; }
        public string? Path { get; private set; }
        public string Body { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Method = request.Method.Method;
            Path = request.RequestUri?.AbsolutePath;
            if (request.Content is not null)
                Body = await request.Content.ReadAsStringAsync(cancellationToken);
            return CreateResponse();
        }

        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
        private static HttpResponseMessage CreateResponse() =>
            new(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"id\":\"ws\"}", Encoding.UTF8, "application/json")
            };
    }
}

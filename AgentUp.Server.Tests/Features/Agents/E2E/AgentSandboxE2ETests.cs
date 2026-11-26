using System.Diagnostics;
using System.Net;
using AgentUp.Capabilities.Opencode.Features.OpencodeCapability.Services;
using AgentUp.Server.Features.Agents.Controllers;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AgentUp.Server.Tests.Features.Agents.E2E;

/// <summary>
/// Real bubblewrap, real Linux mounts: a shell started exactly the way the Server starts an ACP
/// agent tries what a member must not do, and the kernel refuses it. Requires <c>bwrap</c> on
/// PATH, which the Server's Nix shell provides; a host without it fails rather than skips.
/// CI runs this through <c>.github/scripts/run-agent-e2e.sh</c>.
/// </summary>
[TestFixture]
[Category("AgentSandboxE2E")]
[NonParallelizable]
public sealed class AgentSandboxE2ETests
{
    [Test]
    public async Task ADevReviewerCannotWriteItsClone()
    {
        using var host = new Host();

        var result = await host.RunAsync(TeamMemberGrants.DevReviewer, "echo changed > README.md");

        Assert.Multiple(() =>
        {
            Assert.That(result.ExitCode, Is.Not.Zero);
            Assert.That(result.Output, Does.Contain("Read-only file system"));
            Assert.That(File.ReadAllText(Path.Join(host.Clone, "README.md")), Is.EqualTo("original\n"));
        });
    }

    [Test]
    public async Task AnAuthorWritesItsCloneButNotItsGitHistory()
    {
        using var host = new Host();

        var write = await host.RunAsync(TeamMemberGrants.Author, "echo changed > README.md");
        var history = await host.RunAsync(TeamMemberGrants.Author, "echo x > .git/HEAD");

        Assert.Multiple(() =>
        {
            Assert.That(write.ExitCode, Is.Zero, write.Output);
            Assert.That(File.ReadAllText(Path.Join(host.Clone, "README.md")), Is.EqualTo("changed\n"));
            Assert.That(history.ExitCode, Is.Not.Zero);
            Assert.That(history.Output, Does.Contain("Read-only file system"));
        });
    }

    [Test]
    public async Task AnAgentSeesNeitherTheServerDataNorAnotherWorkspace()
    {
        using var host = new Host();

        var result = await host.RunAsync(
            TeamMemberGrants.Author,
            $"test -e '{host.ServerSecret}' && echo data-visible; test -e '{host.OtherClone}' && echo other-visible; echo done");

        Assert.Multiple(() =>
        {
            Assert.That(result.Output, Does.Contain("done"));
            Assert.That(result.Output, Does.Not.Contain("data-visible"));
            Assert.That(result.Output, Does.Not.Contain("other-visible"));
        });
    }

    [Test]
    public async Task AnAgentDoesNotInheritTheServersSecrets()
    {
        using var host = new Host();

        var result = await host.RunAsync(TeamMemberGrants.Author, "echo \"password=${AGENTUP_ADMIN_PASSWORD:-unset}\"");

        Assert.That(result.Output, Does.Contain("password=unset"));
    }

    [Test]
    public async Task AnAgentDoesNotSeeHostEtcOrADefaultRoute()
    {
        using var host = new Host();

        var result = await host.RunAsync(
            TeamMemberGrants.Author,
            "test -e /etc/passwd && echo passwd-visible; test -e /usr/bin/curl && echo curl-visible; echo done");

        Assert.Multiple(() =>
        {
            Assert.That(result.Output, Does.Contain("done"));
            Assert.That(result.Output, Does.Not.Contain("passwd-visible"));
            Assert.That(result.Output, Does.Not.Contain("curl-visible"));
        });
    }

    [Test]
    public async Task AnAgentHasNoHostNetworkInterfaces()
    {
        using var host = new Host();

        var result = await host.RunAsync(
            TeamMemberGrants.Author,
            "while read line; do echo \"$line\"; done < /proc/net/dev && echo done");

        var names = result.Output.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Contains(':') && !line.Contains("Inter-") && !line.StartsWith("face", StringComparison.Ordinal))
            .Select(line => line.Split(':')[0].Trim())
            .Where(name => name.Length > 0)
            .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(result.Output, Does.Contain("done"));
            Assert.That(names, Is.EquivalentTo(new[] { "lo" }));
        });
    }

    [Test]
    [CancelAfter(60_000)]
    public async Task ASandboxedAgentReachesAnOutsideOllamaHostOnlyThroughTheProxy()
    {
        using var host = new Host();
        await using var origin = await OutsideOllamaHost.StartAsync(host.Data);
        var loopback = new AgentLoopbackEndpoint(host.Data);
        var access = new AgentModelProxyAccess();
        await using var proxy = await ModelProxyHost.StartAsync(loopback, access, origin.Url);
        var originPort = new Uri(origin.Url).Port;
        var script = $$"""
            if exec 3<>/dev/tcp/127.0.0.1/{{originPort}}; then echo ORIGIN_REACHABLE; exec 3>&-; else echo ORIGIN_BLOCKED; fi
            fetch() {
              local path=$1
              exec 3<>/dev/tcp/127.0.0.1/{{proxy.Port}} || { echo FAIL_$path; return 1; }
              printf 'GET %s HTTP/1.0\r\nHost: 127.0.0.1\r\nConnection: close\r\nAuthorization: Bearer {{access.Key}}\r\n\r\n' "$path" >&3
              local raw=
              while IFS= read -r -t 3 -n 1 ch <&3; do
                raw="$raw$ch"
              done
              exec 3>&-
              echo "RAW_$path=$raw"
            }
            fetch /api/agent-model/{{access.Key}}/ollama/api/tags
            fetch /api/agent-model/{{access.Key}}/opencode/v1/models
            """;

        var result = await host.RunProxiedAsync(
            TeamMemberGrants.Author,
            script,
            loopback,
            access,
            proxy.BaseUrl);
        TestContext.Out.WriteLine(result.Output);
        TestContext.Out.WriteLine(origin.Hits);

        Assert.Multiple(() =>
        {
            Assert.That(result.Output, Does.Contain("ORIGIN_BLOCKED"), result.Output);
            Assert.That(result.Output, Does.Contain("qwen"), result.Output);
            Assert.That(origin.Hits, Does.Contain("GET /api/tags"));
            Assert.That(origin.Hits, Does.Contain("GET /v1/models"));
            Assert.That(origin.LastAuthorization, Is.EqualTo("Bearer sk-outside"));
        });
    }

    private sealed class Host : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("agent-sandbox-e2e-").FullName;

        public Host()
        {
            Directory.CreateDirectory(Path.Join(Clone, ".git"));
            File.WriteAllText(Path.Join(Clone, ".git", "HEAD"), "ref: refs/heads/main\n");
            File.WriteAllText(Path.Join(Clone, "README.md"), "original\n");
            Directory.CreateDirectory(OtherClone);
            Directory.CreateDirectory(Data);
            File.WriteAllText(ServerSecret, "token");
        }

        public string Clone => Path.Join(_root, "sources", "author-1");
        public string OtherClone => Path.Join(_root, "sources", "author-2");
        public string Data => Path.Join(_root, "data");
        public string ServerSecret => Path.Join(Data, "secrets.json");

        public async Task<(int ExitCode, string Output)> RunAsync(TeamMemberGrants grants, string script)
        {
            var server = Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty);
            server["AGENTUP_ADMIN_PASSWORD"] = "server-secret";
            var sandbox = new BubblewrapAgentSandboxProvider(new AgentCliHomeProvider(Data), Data, serverEnvironment: () => server);
            var launch = sandbox.Prepare(
                new AgentCommand("sh", ["-c", script]),
                new Dictionary<string, string> { ["HOME"] = Path.Join(Data, "agent-cli-home") },
                Clone,
                grants);
            var start = new ProcessStartInfo(launch.FileName)
            {
                WorkingDirectory = launch.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var argument in launch.Arguments) start.ArgumentList.Add(argument);
            start.Environment.Clear();
            foreach (var pair in launch.Environment) start.Environment[pair.Key] = pair.Value;
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, await output + await error);
        }

        public async Task<(int ExitCode, string Output)> RunProxiedAsync(
            TeamMemberGrants grants,
            string script,
            AgentLoopbackEndpoint loopback,
            AgentModelProxyAccess proxy,
            string loopbackBaseUrl)
        {
            var server = Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty);
            server["AGENTUP_ADMIN_PASSWORD"] = "server-secret";
            var sandbox = new BubblewrapAgentSandboxProvider(
                new AgentCliHomeProvider(Data),
                Data,
                serverEnvironment: () => server,
                loopback: loopback,
                proxy: proxy,
                loopbackBaseUrl: () => loopbackBaseUrl);
            var launch = sandbox.Prepare(
                new AgentCommand("bash", ["-c", script]),
                new Dictionary<string, string> { ["HOME"] = Path.Join(Data, "agent-cli-home") },
                Clone,
                grants);
            var start = new ProcessStartInfo(launch.FileName)
            {
                WorkingDirectory = launch.WorkingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            foreach (var argument in launch.Arguments) start.ArgumentList.Add(argument);
            start.Environment.Clear();
            foreach (var pair in launch.Environment) start.Environment[pair.Key] = pair.Value;
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, await output + await error);
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }

    /// <summary>
    /// An Ollama-compatible HTTP host started with the system Python, not the Server process
    /// and not bubblewrap. The sandboxed agent has to reach it through the Server proxy.
    /// </summary>
    private sealed class OutsideOllamaHost : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly string _hits;

        private OutsideOllamaHost(Process process, string url, string hits)
        {
            _process = process;
            Url = url;
            _hits = hits;
        }

        public string Url { get; }
        public string Hits => File.Exists(_hits) ? File.ReadAllText(_hits) : "";
        public string LastAuthorization => Hits.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Split(" auth=", 2))
            .LastOrDefault(parts => parts.Length == 2)?[1]
            .Trim() ?? "";

        public static async Task<OutsideOllamaHost> StartAsync(string data)
        {
            var script = Path.Join(data, "ollama-origin.py");
            var hits = Path.Join(data, "ollama-hits.log");
            var urlFile = Path.Join(data, "ollama-url.txt");
            File.WriteAllText(script, """
                from http.server import BaseHTTPRequestHandler, HTTPServer
                import os

                hits = os.environ["HIT_LOG"]
                url_file = os.environ["URL_FILE"]

                class Handler(BaseHTTPRequestHandler):
                    def _send(self, body):
                        with open(hits, "a", encoding="utf-8") as log:
                            log.write(f"HIT {self.command} {self.path} auth={self.headers.get('Authorization', '')}\n")
                        self.send_response(200)
                        self.send_header("Content-Type", "application/json")
                        self.end_headers()
                        self.wfile.write(body)

                    def do_GET(self):
                        if self.path.startswith("/api/tags"):
                            self._send(b'{"models":[{"name":"qwen"}]}')
                            return
                        self._send(b'{"data":[{"id":"qwen"}]}')

                    def do_POST(self):
                        length = int(self.headers.get("Content-Length") or 0)
                        if length:
                            self.rfile.read(length)
                        self._send(b'{"ok":true}')

                    def log_message(self, format, *args):
                        return

                server = HTTPServer(("127.0.0.1", 0), Handler)
                with open(url_file, "w", encoding="utf-8") as written:
                    written.write(f"http://127.0.0.1:{server.server_address[1]}")
                server.serve_forever()
                """);
            var start = new ProcessStartInfo("/usr/bin/python3", [script])
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };
            start.Environment.Clear();
            start.Environment["PATH"] = "/usr/bin:/bin";
            start.Environment["PYTHONUNBUFFERED"] = "1";
            start.Environment["HIT_LOG"] = hits;
            start.Environment["URL_FILE"] = urlFile;
            var process = Process.Start(start)
                ?? throw new InvalidOperationException("Could not start the outside Ollama-compatible host.");
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline && !File.Exists(urlFile))
                await Task.Delay(20);
            if (!File.Exists(urlFile))
            {
                process.Kill();
                throw new InvalidOperationException(
                    $"The outside Ollama-compatible host did not bind. {await process.StandardError.ReadToEndAsync()}");
            }

            return new OutsideOllamaHost(process, File.ReadAllText(urlFile).Trim(), hits);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_process.HasExited)
                _process.Kill();
            await _process.WaitForExitAsync();
            _process.Dispose();
        }
    }

    private sealed class ModelProxyHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private ModelProxyHost(WebApplication app, string baseUrl, int port)
        {
            _app = app;
            BaseUrl = baseUrl;
            Port = port;
        }

        public string BaseUrl { get; }
        public int Port { get; }

        public static async Task<ModelProxyHost> StartAsync(
            AgentLoopbackEndpoint loopback,
            AgentModelProxyAccess access,
            string originUrl)
        {
            var settings = new CapabilityModuleHarness.MemorySettings();
            settings.Write("opencode", new Dictionary<string, string>
            {
                ["endpoint"] = originUrl,
                ["token"] = "sk-outside"
            });
            var urls = string.Join(';', AgentLoopbackEndpoint.CombineHostingUrls(
                "http://127.0.0.1:0",
                loopback.SocketPath));
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = [$"--urls={urls}"],
                EnvironmentName = Environments.Development
            });
            builder.Services.AddControllers().AddApplicationPart(typeof(AgentModelProxyHttpController).Assembly);
            builder.Services.AddSingleton(access);
            builder.Services.AddSingleton<IAgentModelProxyDestinations>(_ =>
            {
                var modules = CapabilityModuleHarness.CreateService(
                    enabled: true,
                    modules: [new OpencodeAgentCapability()],
                    nixAvailable: false,
                    settings: settings);
                return new AgentModelProxyDestinations(
                    new CapabilityModulesController(modules),
                    new ConfigurationBuilder()
                        .AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["AGENTUP_OLLAMA_URL"] = originUrl
                        })
                        .Build());
            });
            builder.Services.AddSingleton<AgentModelProxyForwarder>();
            builder.Services.AddSingleton<AgentModelProxyService>();
            var app = builder.Build();
            app.MapControllers();
            await app.StartAsync();
            var tcp = app.Urls.FirstOrDefault(url =>
                url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !url.Contains("://unix:", StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"The proxy lost its TCP listener. urls={string.Join(";", app.Urls)}");
            if (!File.Exists(loopback.SocketPath))
                throw new InvalidOperationException(
                    $"Kestrel did not create {loopback.SocketPath}. urls={string.Join(";", app.Urls)}");
            var bound = new Uri(tcp);
            return new ModelProxyHost(app, $"http://127.0.0.1:{bound.Port}", bound.Port);
        }

        public async ValueTask DisposeAsync()
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}

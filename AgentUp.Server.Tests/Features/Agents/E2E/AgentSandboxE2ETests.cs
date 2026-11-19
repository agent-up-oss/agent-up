using System.Diagnostics;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.E2E;

/// <summary>
/// Real bubblewrap, real Linux mounts: a shell started exactly the way the Server starts an ACP
/// agent tries what a member must not do, and the kernel refuses it. Requires <c>bwrap</c> on
/// PATH, which the Server's Nix shell provides; a host without it fails rather than skips.
/// CI runs this through <c>.github/scripts/run-agent-sandbox-e2e.sh</c>.
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

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }
}

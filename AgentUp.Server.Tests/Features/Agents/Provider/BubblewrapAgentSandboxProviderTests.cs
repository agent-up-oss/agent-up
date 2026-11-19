using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

/// <summary>
/// The launch the Server builds for a sandboxed agent. Nothing here starts bubblewrap: a stand-in
/// executable named <c>bwrap</c> on the test PATH is enough to resolve it. The real confinement is
/// proven in <c>AgentSandboxE2ETests</c>.
/// </summary>
[TestFixture]
public sealed class BubblewrapAgentSandboxProviderTests
{
    [Test]
    public void Prepare_refusesToStartWithoutBubblewrap()
    {
        using var host = new SandboxHost(withBubblewrap: false);

        var refused = Assert.Throws<AgentSandboxUnavailableException>(
            () => host.Prepare(TeamMemberGrants.Author));

        Assert.That(refused!.Message, Does.Contain("bubblewrap"));
    }

    [Test]
    public void Prepare_wrapsTheAgentInBubblewrapWithTheCloneWritableForAnAuthor()
    {
        using var host = new SandboxHost(withBubblewrap: true);

        var launch = host.Prepare(TeamMemberGrants.Author);

        Assert.Multiple(() =>
        {
            Assert.That(Path.GetFileName(launch.FileName), Is.EqualTo("bwrap"));
            Assert.That(BindOf(launch, host.Clone), Is.EqualTo("--bind"));
            Assert.That(BindOf(launch, Path.Join(host.Clone, ".git")), Is.EqualTo("--ro-bind"));
            Assert.That(launch.Arguments, Does.Contain("--cap-drop"));
            Assert.That(launch.Arguments, Does.Not.Contain("--die-with-parent"),
                "that signal follows the forking thread, which .NET retires while the agent still runs");
            Assert.That(launch.Arguments.SkipWhile(argument => argument != "--").Skip(1).First(), Is.EqualTo(host.Agent));
            Assert.That(launch.Arguments[^1], Is.EqualTo("acp"));
        });
    }

    [Test]
    public void Prepare_givesADevReviewerAReadOnlyClone()
    {
        using var host = new SandboxHost(withBubblewrap: true);

        var launch = host.Prepare(TeamMemberGrants.DevReviewer);

        Assert.That(BindOf(launch, host.Clone), Is.EqualTo("--ro-bind"));
    }

    [Test]
    public void Prepare_neverMountsTheServerDataDirectoryOnlyTheAgentHome()
    {
        using var host = new SandboxHost(withBubblewrap: true);

        var launch = host.Prepare(TeamMemberGrants.Author);

        Assert.Multiple(() =>
        {
            Assert.That(launch.Arguments, Does.Not.Contain(host.Data));
            Assert.That(BindOf(launch, Path.Join(host.Data, "agent-cli-home")), Is.EqualTo("--bind"));
        });
    }

    [Test]
    public void Prepare_dropsServerSecretsFromTheAgentEnvironment()
    {
        using var host = new SandboxHost(withBubblewrap: true);

        var launch = host.Prepare(TeamMemberGrants.Author);

        Assert.Multiple(() =>
        {
            Assert.That(launch.Environment.Keys, Does.Not.Contain("AGENTUP_ADMIN_PASSWORD"));
            Assert.That(launch.Environment["OPENCODE_CONFIG_CONTENT"], Is.EqualTo("{}"));
            Assert.That(launch.WorkingDirectory, Is.EqualTo(host.Clone));
        });
    }

    [Test]
    public void Prepare_mountsBothGitDirectoriesOfALinkedWorktreeReadOnly()
    {
        using var host = new SandboxHost(withBubblewrap: true, linkedWorktree: true);

        var launch = host.Prepare(TeamMemberGrants.Author);

        Assert.Multiple(() =>
        {
            Assert.That(BindOf(launch, host.LinkedGitDirectory), Is.EqualTo("--ro-bind"));
            Assert.That(BindOf(launch, host.CommonGitDirectory), Is.EqualTo("--ro-bind"));
        });
    }

    [Test]
    public void PackageRoot_isTheOutermostNodeModulesOfAnNpmCli()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BubblewrapAgentSandboxProvider.PackageRoot("/opt/tools/node_modules/opencode-ai/bin/opencode"),
                Is.EqualTo("/opt/tools/node_modules"));
            Assert.That(BubblewrapAgentSandboxProvider.PackageRoot("/opt/tools/bin/codex"), Is.EqualTo("/opt/tools/bin"));
        });
    }

    /// <summary>The flag that binds <paramref name="path"/>, or null when it is not mounted.</summary>
    private static string? BindOf(AgentSandboxLaunch launch, string path)
    {
        var arguments = launch.Arguments;
        var flags = Enumerable.Range(0, arguments.Count - 2)
            .Where(index => arguments[index] is "--bind" or "--ro-bind"
                            && arguments[index + 1] == path
                            && arguments[index + 2] == path)
            .Select(index => arguments[index])
            .ToArray();
        return flags.LastOrDefault();
    }

    private sealed class SandboxHost : IDisposable
    {
        private readonly string _root = Directory.CreateTempSubdirectory("agent-sandbox-").FullName;

        public SandboxHost(bool withBubblewrap, bool linkedWorktree = false)
        {
            Directory.CreateDirectory(Tools);
            Directory.CreateDirectory(Clone);
            Directory.CreateDirectory(Data);
            Executable(Agent);
            if (withBubblewrap)
                Executable(Path.Join(Tools, "bwrap"));
            if (!linkedWorktree)
            {
                Directory.CreateDirectory(Path.Join(Clone, ".git"));
                return;
            }
            Directory.CreateDirectory(LinkedGitDirectory);
            Directory.CreateDirectory(CommonGitDirectory);
            File.WriteAllText(Path.Join(Clone, ".git"), $"gitdir: {LinkedGitDirectory}\n");
            File.WriteAllText(Path.Join(LinkedGitDirectory, "commondir"), "../..\n");
        }

        public string Tools => Path.Join(_root, "tools");
        public string Agent => Path.Join(Tools, "opencode");
        public string Clone => Path.Join(_root, "clones", "author-1");
        public string Data => Path.Join(_root, "data");
        public string CommonGitDirectory => Path.Join(_root, "main", ".git");
        public string LinkedGitDirectory => Path.Join(CommonGitDirectory, "worktrees", "author-1");

        public AgentSandboxLaunch Prepare(TeamMemberGrants grants)
        {
            var server = new Dictionary<string, string>
            {
                ["PATH"] = Tools,
                ["AGENTUP_ADMIN_PASSWORD"] = "secret"
            };
            var provider = new BubblewrapAgentSandboxProvider(
                new AgentCliHomeProvider(Data),
                Data,
                serverEnvironment: () => server);
            return provider.Prepare(
                new AgentCommand("opencode", ["acp"]),
                new Dictionary<string, string> { ["OPENCODE_CONFIG_CONTENT"] = "{}" },
                Clone,
                grants);
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);

        private static void Executable(string path)
        {
            File.WriteAllText(path, "#!/bin/sh\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}

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
    public void Prepare_runsTheExecutableByItsLinkNameAndMountsWhereTheLinkPoints()
    {
        using var host = new SandboxHost(withBubblewrap: true);
        var link = host.Link("nix-shell", host.Multicall);

        var launch = host.Prepare(TeamMemberGrants.Author, command: "nix-shell");

        // nix-shell is a link to the multi-call nix binary, which reads its mode from argv[0].
        Assert.Multiple(() =>
        {
            Assert.That(launch.Arguments.SkipWhile(argument => argument != "--").Skip(1).First(), Is.EqualTo(link));
            Assert.That(BindOf(launch, Path.GetDirectoryName(host.Multicall)!), Is.EqualTo("--ro-bind"));
        });
    }

    [TestCase(false, "--bind")]
    [TestCase(true, "--ro-bind")]
    public void Prepare_leavesTheNixStoreWritableUnlessADaemonSocketIsListening(bool socket, string store)
    {
        using var host = new SandboxHost(withBubblewrap: true);
        host.Nix(socket);

        var launch = host.Prepare(TeamMemberGrants.Author, root: host.HostRoot);

        // An installer leaves the socket directory behind on single-user hosts: only the socket
        // means a daemon owns the store.
        Assert.Multiple(() =>
        {
            Assert.That(BindOf(launch, "/nix"), Is.EqualTo(store));
            Assert.That(BindOf(launch, "/nix/var/nix/daemon-socket"), Is.EqualTo(socket ? "--bind" : null));
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
    public void Prepare_neverMountsTheServerDataDirectoryOnlyTheWorkspaceAgentHome()
    {
        using var host = new SandboxHost(withBubblewrap: true);

        var launch = host.Prepare(TeamMemberGrants.Author);
        var workspaceHome = new AgentCliHomeProvider(host.Data).WorkspaceHome(host.Clone);

        Assert.Multiple(() =>
        {
            Assert.That(launch.Arguments, Does.Not.Contain(host.Data));
            Assert.That(BindOf(launch, workspaceHome), Is.EqualTo("--bind"));
            Assert.That(launch.Environment["HOME"], Is.EqualTo(workspaceHome));
        });
    }

    [Test]
    public void Prepare_mountsNonSystemPathDirectoriesSoAShimCanExecItsBinary()
    {
        using var host = new SandboxHost(withBubblewrap: true);
        var packages = host.MakeDirectory("packages");
        host.ExecutableFileIn(packages, "test-agent");
        var server = new Dictionary<string, string>
        {
            ["PATH"] = string.Join(Path.PathSeparator, [host.Tools, packages, "/usr/bin"])
        };
        var provider = new BubblewrapAgentSandboxProvider(
            new AgentCliHomeProvider(host.Data),
            host.Data,
            serverEnvironment: () => server,
            linux: true);

        var launch = provider.Prepare(
            new AgentCommand("opencode", ["acp"]),
            new Dictionary<string, string>(),
            host.Clone,
            TeamMemberGrants.Author);

        Assert.Multiple(() =>
        {
            Assert.That(BindOf(launch, packages), Is.EqualTo("--ro-bind"));
            Assert.That(BindOf(launch, "/usr/bin"), Is.Null);
        });
    }

    [Test]
    public void Prepare_unsharesTheNetworkAndDoesNotBindHostEtc()
    {
        using var host = new SandboxHost(withBubblewrap: true);

        var launch = host.Prepare(TeamMemberGrants.Author);

        Assert.Multiple(() =>
        {
            Assert.That(launch.Arguments, Does.Contain("--unshare-net"));
            Assert.That(BindOf(launch, "/etc"), Is.Null);
            Assert.That(BindOf(launch, "/usr"), Is.Null);
            Assert.That(launch.Arguments, Does.Contain("--dir"));
        });
    }

    [Test]
    public void Prepare_pointsModelHttpAtTheServerProxyWhenLoopbackIsReady()
    {
        using var host = new SandboxHost(withBubblewrap: true);
        var loopback = new AgentLoopbackEndpoint(host.Data);
        File.Create(loopback.SocketPath).Dispose();
        var proxy = new AgentModelProxyAccess();
        var sandbox = new BubblewrapAgentSandboxProvider(
            new AgentCliHomeProvider(host.Data),
            host.Data,
            serverEnvironment: () => new Dictionary<string, string>
            {
                ["PATH"] = host.Tools,
                ["AGENTUP_ADMIN_PASSWORD"] = "secret"
            },
            loopback: loopback,
            proxy: proxy,
            loopbackBaseUrl: () => "http://127.0.0.1:5001");

        var launch = sandbox.Prepare(
            new AgentCommand("opencode", ["acp"]),
            new Dictionary<string, string>
            {
                ["OPENCODE_CONFIG_CONTENT"] =
                    """{"provider":{"agent-up":{"options":{"baseURL":"http://ollama.test/v1"}}}}"""
            },
            host.Clone,
            TeamMemberGrants.Author);

        Assert.Multiple(() =>
        {
            Assert.That(launch.Arguments, Does.Contain(AgentSandboxLoopbackBridge.Flag));
            Assert.That(launch.Environment["ANTHROPIC_BASE_URL"],
                Does.Contain($"/api/agent-model/{proxy.Key}/claude"));
            Assert.That(launch.Environment["OPENAI_BASE_URL"],
                Does.Contain($"/api/agent-model/{proxy.Key}/codex/v1"));
            Assert.That(launch.Environment["OPENCODE_CONFIG_CONTENT"],
                Does.Contain($"/api/agent-model/{proxy.Key}/opencode/v1"));
            Assert.That(launch.Environment["AGENT_UP_OPENCODE_API_KEY"], Is.EqualTo(proxy.Key));
            Assert.That(BindOf(launch, Path.GetDirectoryName(loopback.SocketPath)!), Is.EqualTo("--bind"));
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
    public void Prepare_offLinuxStartsTheAgentDirectlyButStillWithoutTheServersSecrets()
    {
        using var host = new SandboxHost(withBubblewrap: false);

        var launch = host.Prepare(TeamMemberGrants.Author, linux: false);

        Assert.Multiple(() =>
        {
            Assert.That(launch.FileName, Is.EqualTo("opencode"), "bubblewrap is Linux only, and no other host is refused");
            Assert.That(launch.Arguments, Is.EqualTo(new[] { "acp" }));
            Assert.That(launch.Environment.Keys, Does.Not.Contain("AGENTUP_ADMIN_PASSWORD"));
        });
    }

    [Test]
    public void Prepare_bindsAShebangInterpreterAndItsPathCommandWithoutTheSystemBinDirectory()
    {
        using var host = new SandboxHost(withBubblewrap: true);
        var script = host.Script("cli", "#!/usr/bin/env node\n");
        host.ExecutableFile("node");

        var launch = host.Prepare(TeamMemberGrants.Author, command: "cli");

        Assert.Multiple(() =>
        {
            Assert.That(BindOf(launch, "/usr/bin/env"), Is.EqualTo("--ro-bind"));
            Assert.That(BindOf(launch, Path.Join(host.Tools, "node")), Is.EqualTo("--ro-bind"));
            Assert.That(launch.Arguments, Does.Not.Contain("/usr/bin"));
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
        public string Multicall => Path.Join(_root, "store", "nix", "bin", "nix");
        public string HostRoot => Path.Join(_root, "host");

        /// <summary>A Nix install under <see cref="HostRoot"/>, with or without a listening daemon.</summary>
        public void Nix(bool socket)
        {
            var directory = Path.Join(HostRoot, "nix", "var", "nix", "daemon-socket");
            Directory.CreateDirectory(directory);
            if (socket)
                File.WriteAllText(Path.Join(directory, "socket"), string.Empty);
        }

        /// <summary>A link named <paramref name="name"/> on the tools PATH to <paramref name="target"/>.</summary>
        public string Link(string name, string target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            Executable(target);
            var link = Path.Join(Tools, name);
            File.CreateSymbolicLink(link, target);
            return link;
        }
        public string Clone => Path.Join(_root, "clones", "author-1");
        public string Data => Path.Join(_root, "data");
        public string CommonGitDirectory => Path.Join(_root, "main", ".git");
        public string LinkedGitDirectory => Path.Join(CommonGitDirectory, "worktrees", "author-1");

        public AgentSandboxLaunch Prepare(TeamMemberGrants grants, bool linux = true, string command = "opencode", string root = "/")
        {
            var server = new Dictionary<string, string>
            {
                ["PATH"] = Tools,
                ["AGENTUP_ADMIN_PASSWORD"] = "secret"
            };
            var provider = new BubblewrapAgentSandboxProvider(
                new AgentCliHomeProvider(Data),
                Data,
                serverEnvironment: () => server,
                root: root,
                linux: linux);
            return provider.Prepare(
                new AgentCommand(command, ["acp"]),
                new Dictionary<string, string> { ["OPENCODE_CONFIG_CONTENT"] = "{}" },
                Clone,
                grants);
        }

        public string Script(string name, string contents)
        {
            var path = Path.Join(Tools, name);
            File.WriteAllText(path, contents);
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            return path;
        }

        public string ExecutableFile(string name)
        {
            var path = Path.Join(Tools, name);
            Executable(path);
            return path;
        }

        public string MakeDirectory(string name)
        {
            var path = Path.Join(_root, name);
            Directory.CreateDirectory(path);
            return path;
        }

        public string ExecutableFileIn(string directory, string name)
        {
            var path = Path.Join(directory, name);
            Executable(path);
            return path;
        }

        public void Dispose() => Directory.Delete(_root, recursive: true);

        private static void Executable(string path)
        {
            File.WriteAllText(path, "#!/bin/sh\n");
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}

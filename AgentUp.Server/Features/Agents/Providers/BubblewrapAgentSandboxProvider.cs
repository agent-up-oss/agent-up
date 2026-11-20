using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Confines an ACP agent with bubblewrap. The Server runs inside its Nix shell, which puts
/// <c>bwrap</c> on its PATH; a Linux host without it cannot start an agent at all. bubblewrap is
/// Linux only: on another host the agent still starts with only the allowlisted environment, but
/// its filesystem is not confined.
/// </summary>
public sealed class BubblewrapAgentSandboxProvider : IAgentSandbox
{
    public const string Executable = "bwrap";
    private const string NixRoot = "/nix";
    private const string NixDaemonSocket = "/nix/var/nix/daemon-socket";

    private static readonly string[] SystemRootCandidates =
    [
        "/usr", "/bin", "/sbin", "/lib", "/lib32", "/lib64", "/libx32", "/etc", "/opt",
        "/run/current-system", "/run/systemd/resolve"
    ];

    private static readonly string[] CertificateVariables = ["SSL_CERT_FILE", "NIX_SSL_CERT_FILE", "NODE_EXTRA_CA_CERTS"];

    private readonly AgentCliHomeProvider _home;
    private readonly string _capabilityShells;
    private readonly IAgentRuntimePathProvider? _runtime;
    private readonly Func<IReadOnlyDictionary<string, string>> _serverEnvironment;
    private readonly string _root;
    private readonly bool _linux;

    public BubblewrapAgentSandboxProvider(
        AgentCliHomeProvider home,
        string dataDirectory,
        IAgentRuntimePathProvider? runtime = null,
        Func<IReadOnlyDictionary<string, string>>? serverEnvironment = null,
        string root = "/",
        bool? linux = null)
    {
        _linux = linux ?? OperatingSystem.IsLinux();
        _home = home;
        _capabilityShells = Path.GetFullPath(Path.Join(dataDirectory, "capabilities", "nix"));
        _runtime = runtime;
        _serverEnvironment = serverEnvironment ?? ProcessEnvironment;
        _root = root;
    }

    public AgentSandboxLaunch Prepare(
        AgentCommand command,
        IReadOnlyDictionary<string, string> environment,
        string worktree,
        TeamMemberGrants grants)
    {
        var server = _serverEnvironment();
        if (!_linux)
            return new AgentSandboxLaunch(
                command.FileName,
                command.Arguments,
                AgentSandboxLayout.Environment(server, environment),
                Path.GetFullPath(worktree));
        // The agent's PATH carries the runtime prefixes the Server adds in front of its own.
        var path = environment.GetValueOrDefault("PATH") ?? server.GetValueOrDefault("PATH") ?? string.Empty;
        var bwrap = Resolve(Executable, path)
            ?? throw new AgentSandboxUnavailableException(
                "Agents run sandboxed and this Server cannot find bubblewrap (bwrap). Start the Server through its Nix shell, or install bubblewrap on the host.");
        var executable = Resolve(command.FileName, path)
            ?? throw new InvalidOperationException($"{command.FileName} is not installed or is not on PATH.");

        _home.Ensure();
        var clone = Path.GetFullPath(worktree);
        // A daemon is running only when its socket is there. Installers create the directory on
        // single-user hosts too, and treating that as a daemon left the store read-only to a Nix
        // that has nobody else to write it.
        var daemon = Exists(NixDaemonSocket + "/socket");
        var inputs = new AgentSandboxInputs(
            SystemRootCandidates.Where(Exists).ToArray(),
            Exists(NixRoot) ? NixRoot : null,
            NixWritable: Exists(NixRoot) && !daemon,
            daemon ? NixDaemonSocket : null,
            ReadOnlyPaths(executable, path, server),
            _home.HomePath,
            clone,
            GitDirectories(clone));
        var mounts = AgentSandboxLayout.Mounts(inputs, grants);

        // No --die-with-parent: Linux delivers that signal when the forking *thread* exits, and
        // .NET forks from a thread-pool thread it later retires, which killed a live agent
        // mid-turn. The Server stops its agents itself.
        string[] arguments =
        [
            "--unshare-pid", "--unshare-ipc", "--unshare-uts",
            "--cap-drop", "ALL",
            "--dev", "/dev", "--proc", "/proc", "--tmpfs", "/tmp",
            .. mounts.SelectMany(mount => new[] { mount.Writable ? "--bind" : "--ro-bind", mount.Path, mount.Path }),
            "--chdir", clone,
            "--", executable,
            .. command.Arguments
        ];
        return new AgentSandboxLaunch(
            bwrap,
            arguments,
            AgentSandboxLayout.Environment(server, environment),
            clone);
    }

    /// <summary>
    /// The tools the agent is given: the directories on the Server's PATH, the capability shells
    /// it launches through, the runtime prefixes, the .NET runtime, certificate bundles, and the
    /// package its own executable lives in.
    /// </summary>
    private IReadOnlyList<string> ReadOnlyPaths(string executable, string path, IReadOnlyDictionary<string, string> server)
    {
        var paths = new List<string>();
        paths.AddRange(path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
        paths.AddRange(_runtime?.PathPrefixes() ?? []);
        paths.Add(_capabilityShells);
        if (server.TryGetValue("DOTNET_ROOT", out var dotnet))
            paths.Add(dotnet);
        paths.AddRange(CertificateVariables
            .Where(server.ContainsKey)
            .Select(name => server[name]));
        paths.Add(Path.GetDirectoryName(executable)!);
        paths.Add(Path.GetDirectoryName(RealPath(executable))!);
        paths.Add(PackageRoot(RealPath(executable)));
        return paths
            .Where(Path.IsPathRooted)
            .Select(Path.GetFullPath)
            .Where(Exists)
            .Where(candidate => !UnderAny(candidate, SystemRootCandidates.Append(NixRoot)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// An npm-installed CLI is a script whose code sits in its <c>node_modules</c> tree, often in
    /// a sibling package, so the outermost <c>node_modules</c> is the unit to expose.
    /// </summary>
    internal static string PackageRoot(string executable)
    {
        const string marker = "/node_modules/";
        var index = executable.IndexOf(marker, StringComparison.Ordinal);
        return index < 0
            ? Path.GetDirectoryName(executable)!
            : executable[..(index + marker.Length - 1)];
    }

    /// <summary>A clone's Git directory, or for a linked worktree both its own and the shared one.</summary>
    private static IReadOnlyList<string> GitDirectories(string clone)
    {
        var git = Path.Join(clone, ".git");
        if (Directory.Exists(git))
            return [git];
        if (!File.Exists(git))
            return [];
        var pointer = File.ReadAllText(git).Trim();
        const string prefix = "gitdir:";
        if (!pointer.StartsWith(prefix, StringComparison.Ordinal))
            return [];
        var own = Path.GetFullPath(pointer[prefix.Length..].Trim(), clone);
        var commonFile = Path.Join(own, "commondir");
        var directories = new List<string> { own };
        if (File.Exists(commonFile))
            directories.Add(Path.GetFullPath(File.ReadAllText(commonFile).Trim(), own));
        return directories.Where(Directory.Exists).ToArray();
    }

    /// <summary>
    /// The executable as found on PATH, symlink and all. Exec'ing the link target instead would
    /// change <c>argv[0]</c>, and a multi-call binary picks its mode from that name: <c>nix-shell</c>
    /// is a link to <c>nix</c>, so a capability launched through its Nix shell would run as plain
    /// <c>nix</c>. The target's directory is mounted separately, so the link resolves in the sandbox.
    /// </summary>
    private static string? Resolve(string fileName, string path)
    {
        if (Path.IsPathRooted(fileName))
            return File.Exists(fileName) ? Path.GetFullPath(fileName) : null;
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.GetFullPath(Path.Join(directory, fileName)))
            .FirstOrDefault(File.Exists);
    }

    private static string RealPath(string path) =>
        new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? Path.GetFullPath(path);

    private bool Exists(string path)
    {
        var onHost = Path.Join(_root, path);
        return Directory.Exists(onHost) || File.Exists(onHost);
    }

    private static bool UnderAny(string path, IEnumerable<string> roots) =>
        roots.Any(root => path.Equals(root, StringComparison.Ordinal)
                          || path.StartsWith(root + "/", StringComparison.Ordinal));

    private static IReadOnlyDictionary<string, string> ProcessEnvironment() =>
        System.Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(entry => (string)entry.Key, entry => entry.Value?.ToString() ?? string.Empty, StringComparer.Ordinal);
}

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

    private static readonly string[] LibraryRootCandidates =
    [
        "/lib", "/lib32", "/lib64", "/libx32"
    ];

    private static readonly string[] SystemBinDirectories =
    [
        "/usr/bin", "/bin", "/usr/sbin", "/sbin"
    ];

    private static readonly string[] CertificateDirectories =
    [
        "/etc/ssl", "/etc/pki"
    ];

    private static readonly string[] ExtraReadOnlyVariables =
    [
        "AGENTUP_LOOP_INFERENCE_MODEL_ROOT", "AGENT_UP_DEV_ROOT"
    ];

    private static readonly string[] CertificateVariables = ["SSL_CERT_FILE", "NIX_SSL_CERT_FILE", "NODE_EXTRA_CA_CERTS"];

    private readonly AgentCliHomeProvider _home;
    private readonly string _capabilityShells;
    private readonly IAgentRuntimePathProvider? _runtime;
    private readonly Func<IReadOnlyDictionary<string, string>> _serverEnvironment;
    private readonly string _root;
    private readonly bool _linux;
    private readonly AgentLoopbackEndpoint? _loopback;
    private readonly AgentModelProxyAccess? _proxy;
    private readonly Func<string>? _loopbackBaseUrl;

    public BubblewrapAgentSandboxProvider(
        AgentCliHomeProvider home,
        string dataDirectory,
        IAgentRuntimePathProvider? runtime = null,
        Func<IReadOnlyDictionary<string, string>>? serverEnvironment = null,
        string root = "/",
        bool? linux = null,
        AgentLoopbackEndpoint? loopback = null,
        AgentModelProxyAccess? proxy = null,
        Func<string>? loopbackBaseUrl = null)
    {
        _linux = linux ?? OperatingSystem.IsLinux();
        _home = home;
        _capabilityShells = Path.GetFullPath(Path.Join(dataDirectory, "capabilities", "nix"));
        _runtime = runtime;
        _serverEnvironment = serverEnvironment ?? ProcessEnvironment;
        _root = root;
        _loopback = loopback;
        _proxy = proxy;
        _loopbackBaseUrl = loopbackBaseUrl;
    }

    public AgentSandboxLaunch Prepare(
        AgentCommand command,
        IReadOnlyDictionary<string, string> environment,
        string worktree,
        TeamMemberGrants grants)
    {
        var server = _serverEnvironment();
        var workspaceHome = _home.EnsureWorkspaceHome(worktree);
        var agentEnvironment = new Dictionary<string, string>(environment, StringComparer.Ordinal)
        {
            ["HOME"] = workspaceHome
        };
        if (!_linux)
            return new AgentSandboxLaunch(
                command.FileName,
                command.Arguments,
                AgentSandboxLayout.Environment(server, agentEnvironment),
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
            LibraryRootCandidates.Where(Exists).ToArray(),
            Exists(NixRoot) ? NixRoot : null,
            NixWritable: Exists(NixRoot) && !daemon,
            daemon ? NixDaemonSocket : null,
            ReadOnlyPaths(executable, path, server, agentEnvironment),
            workspaceHome,
            clone,
            GitDirectories(clone));
        var mounts = AgentSandboxLayout.Mounts(inputs, grants);
        var prepared = AgentSandboxLayout.Environment(server, agentEnvironment);
        if (_proxy is not null && LoopbackPort() is { } port)
            prepared = AgentModelProxyEnvironment.Apply(prepared, LoopbackUrl(port), _proxy.Key);

        var bridged = LoopbackBridge(executable, command.Arguments, out var agentExecutable);
        // No --die-with-parent: Linux delivers that signal when the forking *thread* exits, and
        // .NET forks from a thread-pool thread it later retires, which killed a live agent
        // mid-turn. The Server stops its agents itself.
        string[] arguments =
        [
            "--unshare-pid", "--unshare-ipc", "--unshare-uts", "--unshare-net",
            "--cap-drop", "ALL",
            "--cap-add", "CAP_NET_ADMIN",
            "--dev", "/dev", "--proc", "/proc", "--tmpfs", "/tmp",
            "--dir", "/etc",
            .. CertificateBinds(),
            .. mounts.SelectMany(mount => new[] { mount.Writable ? "--bind" : "--ro-bind", mount.Path, mount.Path }),
            .. LoopbackBinds(),
            "--chdir", clone,
            "--", agentExecutable,
            .. bridged
        ];
        return new AgentSandboxLaunch(
            bwrap,
            arguments,
            prepared,
            clone);
    }

    private IReadOnlyList<string> LoopbackBridge(string executable, IReadOnlyList<string> arguments, out string agentExecutable)
    {
        agentExecutable = executable;
        if (_loopback is null || !File.Exists(_loopback.SocketPath) || LoopbackPort() is not { } port)
            return arguments;
        var wrapped = AgentSandboxLoopbackBridge.Wrap(_loopback.SocketPath, port, executable, arguments);
        agentExecutable = wrapped[0];
        return wrapped.Skip(1).ToArray();
    }

    private int? LoopbackPort()
    {
        var url = _loopbackBaseUrl?.Invoke();
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;
        return uri.Port;
    }

    private static string LoopbackUrl(int port) => $"http://127.0.0.1:{port}";

    /// <summary>
    /// Connecting to a Unix socket needs write permission on the inode. A read-only bind of the
    /// socket file leaves the bridge unable to reach Kestrel.
    /// </summary>
    private IEnumerable<string> LoopbackBinds()
    {
        if (_loopback is null || !File.Exists(_loopback.SocketPath))
            yield break;
        var directory = Path.GetDirectoryName(_loopback.SocketPath);
        if (string.IsNullOrWhiteSpace(directory))
            yield break;
        yield return "--bind";
        yield return directory;
        yield return directory;
    }

    private IEnumerable<string> CertificateBinds()
    {
        foreach (var directory in CertificateDirectories.Where(Exists))
        {
            yield return "--ro-bind";
            yield return directory;
            yield return directory;
        }
    }

    /// <summary>
    /// The tools the agent is given: its own package, capability shells, runtime prefixes, the
    /// .NET runtime, certificate bundles, and the Server process that bridges loopback. Host
    /// <c>/usr/bin</c> is never mounted as a directory, so a coding agent does not inherit curl
    /// and a shell from the Server host.
    /// </summary>
    private IReadOnlyList<string> ReadOnlyPaths(
        string executable,
        string path,
        IReadOnlyDictionary<string, string> server,
        IReadOnlyDictionary<string, string> agent)
    {
        var paths = new List<string>();
        paths.AddRange(_runtime?.PathPrefixes() ?? []);
        paths.Add(_capabilityShells);
        if (server.TryGetValue("DOTNET_ROOT", out var dotnet))
            paths.Add(dotnet);
        paths.AddRange(CertificateVariables
            .Where(server.ContainsKey)
            .Select(name => server[name]));
        AddExecutableClosure(paths, executable, path);
        paths.AddRange(new[] { "python3", "python" }
            .Select(interpreter => Resolve(interpreter, path))
            .Where(found => found is not null)
            .Select(found => found!));

        if (File.Exists(_loopback?.SocketPath ?? ""))
            paths.AddRange(AgentSandboxLoopbackBridge.HostMounts().Where(mount => !IsSystemBin(mount)));
        paths.AddRange(ExtraReadOnlyVariables
            .Select(name => agent.TryGetValue(name, out var extra) || server.TryGetValue(name, out extra) ? extra : null)
            .OfType<string>());

        return paths
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Where(Path.IsPathRooted)
            .Select(Path.GetFullPath)
            .Where(Exists)
            .Where(candidate => !UnderAny(candidate, LibraryRootCandidates.Append(NixRoot)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddExecutableClosure(List<string> paths, string executable, string searchPath)
    {
        paths.Add(executable);
        var real = RealPath(executable);
        paths.Add(real);
        var directory = Path.GetDirectoryName(executable);
        var realDirectory = Path.GetDirectoryName(real);
        if (directory is not null && !IsSystemBin(directory))
            paths.Add(directory);
        if (realDirectory is not null && !IsSystemBin(realDirectory))
            paths.Add(realDirectory);
        var package = PackageRoot(real);
        if (!IsSystemBin(package))
            paths.Add(package);
        AddShebang(paths, real, searchPath);
    }

    /// <summary>
    /// Bind the interpreter a script names, not the directory that holds it. <c>#!/usr/bin/env node</c>
    /// needs <c>/usr/bin/env</c> and <c>node</c> on PATH, not all of <c>/usr/bin</c>.
    /// </summary>
    private static void AddShebang(List<string> paths, string executable, string searchPath)
    {
        if (!TryReadShebang(executable, out var interpreter, out var argument))
            return;
        if (Path.IsPathRooted(interpreter))
            paths.Add(interpreter);
        if (string.IsNullOrWhiteSpace(argument))
            return;
        var resolved = Resolve(argument.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)[0], searchPath);
        if (resolved is not null)
            paths.Add(resolved);
    }

    internal static bool TryReadShebang(string executable, out string interpreter, out string? argument)
    {
        interpreter = "";
        argument = null;
        if (!File.Exists(executable))
            return false;
        using var stream = File.OpenRead(executable);
        var magic = new byte[2];
        if (stream.Read(magic) < 2 || magic[0] != (byte)'#' || magic[1] != (byte)'!')
            return false;
        stream.Position = 0;
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 256, leaveOpen: false);
        var line = reader.ReadLine();
        if (line is null || !line.StartsWith("#!", StringComparison.Ordinal))
            return false;
        var parts = line[2..].Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;
        interpreter = parts[0];
        argument = parts.Length > 1 ? parts[1] : null;
        return true;
    }

    private static bool IsSystemBin(string path)
        => SystemBinDirectories.Any(bin => path.Equals(bin, StringComparison.Ordinal));

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
        var onHost = Path.Join(_root, path.TrimStart('/'));
        if (_root == "/")
            onHost = path;
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

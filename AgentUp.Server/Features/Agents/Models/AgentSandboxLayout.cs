namespace AgentUp.Server.Features.Agents.Models;

/// <summary>One path the sandboxed agent can see, and whether it may write there.</summary>
public sealed record AgentSandboxMount(string Path, bool Writable);

/// <summary>
/// What an ACP agent process can see of the Server's host. The view is an allowlist: a path that
/// is not listed here does not exist inside the sandbox, which is how the Server's data
/// directory, other workspaces' clones and the service account's home stay out of reach.
/// </summary>
/// <param name="SystemRoots">Operating-system directories that exist on this host, such as <c>/usr</c> and <c>/etc</c>. Always read-only.</param>
/// <param name="Nix">The Nix store root, or null when the host has none.</param>
/// <param name="NixWritable">True on a single-user Nix install with no daemon: <c>nix shell</c> then writes the store itself.</param>
/// <param name="NixDaemonSocket">The daemon socket directory on a multi-user install, writable so the agent can ask the daemon to build.</param>
/// <param name="ReadOnlyPaths">Tools the agent is given: its executable, the capability shells, runtime path prefixes, certificates.</param>
/// <param name="AgentHome">The agent CLI home under the Server data directory, where it keeps its own credentials.</param>
/// <param name="Worktree">The workspace clone the agent works in.</param>
/// <param name="GitDirectories">The clone's Git directories. Read-only for every member: commits go through the queue.</param>
public sealed record AgentSandboxInputs(
    IReadOnlyList<string> SystemRoots,
    string? Nix,
    bool NixWritable,
    string? NixDaemonSocket,
    IReadOnlyList<string> ReadOnlyPaths,
    string AgentHome,
    string Worktree,
    IReadOnlyList<string> GitDirectories);

public static class AgentSandboxLayout
{
    /// <summary>
    /// The mounts in the order they must be applied. A later mount over an earlier one wins, so the
    /// clone is bound after the tool paths that might contain it, and the Git directories last of
    /// all so a writable clone still cannot write its own history.
    /// </summary>
    public static IReadOnlyList<AgentSandboxMount> Mounts(AgentSandboxInputs inputs, TeamMemberGrants grants)
    {
        if (!grants.ReadsRepository)
            throw new InvalidOperationException(
                $"A {grants.Kind} has no clone to work in, so it does not run as a sandboxed process.");

        var mounts = new List<AgentSandboxMount>();
        mounts.AddRange(inputs.SystemRoots.Select(path => new AgentSandboxMount(path, false)));
        if (inputs.Nix is not null)
            mounts.Add(new AgentSandboxMount(inputs.Nix, inputs.NixWritable));
        if (inputs.Nix is not null && !inputs.NixWritable && inputs.NixDaemonSocket is not null)
            mounts.Add(new AgentSandboxMount(inputs.NixDaemonSocket, true));
        mounts.AddRange(inputs.ReadOnlyPaths
            .Distinct(StringComparer.Ordinal)
            .Select(path => new AgentSandboxMount(path, false)));
        mounts.Add(new AgentSandboxMount(inputs.AgentHome, true));
        mounts.Add(new AgentSandboxMount(inputs.Worktree, grants.WritesRepository));
        mounts.AddRange(inputs.GitDirectories
            .Distinct(StringComparer.Ordinal)
            .Select(path => new AgentSandboxMount(path, false)));
        return mounts;
    }

    /// <summary>
    /// The Server's own environment variables an agent may inherit. Everything else - the
    /// administrator password, signing keys, forge secrets - is dropped, and the agent receives
    /// only these plus what the Server sets for it.
    /// </summary>
    public static IReadOnlySet<string> InheritedEnvironment { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "PATH", "LANG", "LC_ALL", "LC_CTYPE", "TZ", "TERM", "USER", "LOGNAME",
        "SSL_CERT_FILE", "NIX_SSL_CERT_FILE", "NODE_EXTRA_CA_CERTS", "LOCALE_ARCHIVE",
        "NIX_PATH", "NIX_REMOTE", "NIX_CONF_DIR", "DOTNET_ROOT",
        "HTTP_PROXY", "HTTPS_PROXY", "NO_PROXY", "http_proxy", "https_proxy", "no_proxy"
    };

    /// <summary>The environment the sandboxed agent starts with.</summary>
    public static IReadOnlyDictionary<string, string> Environment(
        IReadOnlyDictionary<string, string> server,
        IReadOnlyDictionary<string, string> agent)
    {
        var environment = server
            .Where(pair => InheritedEnvironment.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        environment["TMPDIR"] = "/tmp";
        foreach (var pair in agent)
            environment[pair.Key] = pair.Value;
        return environment;
    }
}

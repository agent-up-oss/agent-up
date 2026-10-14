using AgentUp.Server.Features.Forge.Interfaces;
using AgentUp.Server.Features.Forge.Models;
using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Server.Features.Forge.Providers;

/// <summary>
/// Reads origin and HEAD from the checkout. Forge never shells out; the files Git already wrote
/// are enough to name owner, repository, and branch.
/// </summary>
public sealed class ForgeGitRemoteResolver(GitDirectoryProvider gitDirectories) : IForgeGitRemoteResolver
{
    public ForgeGitRemote? Resolve(string worktreePath)
    {
        var gitDirectory = gitDirectories.Resolve(worktreePath);
        if (gitDirectory is null)
            return null;

        var url = ReadOriginUrl(Path.Join(gitDirectory, "config"));
        if (url is null || !TryParse(url, out var owner, out var repository))
            return null;

        return new ForgeGitRemote(owner, repository, ReadBranch(gitDirectory), url);
    }

    internal static bool TryParse(string url, out string owner, out string repository)
    {
        owner = "";
        repository = "";
        var trimmed = url.Trim().TrimEnd('/');
        if (trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[..^4];

        if (trimmed.StartsWith("git@", StringComparison.Ordinal) && trimmed.Contains(':'))
        {
            var path = trimmed[(trimmed.IndexOf(':') + 1)..];
            return Split(path, out owner, out repository);
        }

        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
            && uri.Host.Contains("github", StringComparison.OrdinalIgnoreCase))
        {
            return Split(uri.AbsolutePath.Trim('/'), out owner, out repository);
        }

        return false;
    }

    private static bool Split(string path, out string owner, out string repository)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            owner = "";
            repository = "";
            return false;
        }

        owner = parts[0];
        repository = parts[1];
        return true;
    }

    private static string? ReadOriginUrl(string configPath)
    {
        if (!File.Exists(configPath))
            return null;

        var inOrigin = false;
        foreach (var raw in File.ReadAllLines(configPath))
        {
            var line = raw.Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                inOrigin = line.Equals("[remote \"origin\"]", StringComparison.OrdinalIgnoreCase);
                continue;
            }

            if (inOrigin && line.StartsWith("url", StringComparison.OrdinalIgnoreCase))
            {
                var separator = line.IndexOf('=');
                if (separator >= 0)
                    return line[(separator + 1)..].Trim();
            }
        }

        return null;
    }

    private static string ReadBranch(string gitDirectory)
    {
        var headPath = Path.Join(gitDirectory, "HEAD");
        if (!File.Exists(headPath))
            return "";

        var head = File.ReadAllText(headPath).Trim();
        const string prefix = "ref: refs/heads/";
        return head.StartsWith(prefix, StringComparison.Ordinal) ? head[prefix.Length..] : "";
    }
}

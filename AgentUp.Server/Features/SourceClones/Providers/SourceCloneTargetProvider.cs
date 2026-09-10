using System.Text.RegularExpressions;
using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Interfaces;

namespace AgentUp.Server.Features.SourceClones.Providers;

public sealed partial class SourceCloneTargetProvider : ISourceCloneTargetProvider
{
    private readonly ISourceCloneRootProvider _root;

    public SourceCloneTargetProvider(ISourceCloneRootProvider root)
    {
        _root = root;
    }

    public SourceCloneTarget Resolve(CloneSourceRequest request)
    {
        var repository = NormalizeRepository(request.Repository);
        var branch = NormalizeBranch(request.Branch);
        var directoryName = DeriveDirectoryName(repository);
        var destinationPath = ResolveDestinationPath(directoryName);

        return new SourceCloneTarget(repository, branch, directoryName, destinationPath);
    }

    public bool DestinationExists(SourceCloneTarget target)
        => Directory.Exists(target.DestinationPath) || File.Exists(target.DestinationPath);

    private string ResolveDestinationPath(string directoryName)
    {
        var rootFullPath = Path.GetFullPath(_root.GetRoot());
        var destination = Path.GetFullPath(Path.Join(rootFullPath, directoryName));
        var relative = Path.GetRelativePath(rootFullPath, destination);
        if (relative != directoryName)
            throw new InvalidOperationException("Source clone destination must stay under the source clones root.");

        return destination;
    }

    private static string NormalizeRepository(string? repository)
    {
        var trimmed = (repository ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new InvalidOperationException("Repository is required.");

        if (trimmed.Length > 512
            || trimmed.Any(char.IsControl)
            || trimmed.StartsWith('-')
            || trimmed.Contains("://", StringComparison.Ordinal) && !HasSupportedScheme(trimmed))
        {
            throw new InvalidOperationException("Repository must be an http, https, ssh, or git remote URL.");
        }

        if (!RemoteUrl().IsMatch(trimmed) && !ScpLikeRemote().IsMatch(trimmed))
            throw new InvalidOperationException("Repository must be an http, https, ssh, or git remote URL.");

        return trimmed;
    }

    private static bool HasSupportedScheme(string repository)
        => repository.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
           || repository.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
           || repository.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase)
           || repository.StartsWith("git://", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeBranch(string? branch)
    {
        var trimmed = (branch ?? string.Empty).Trim();
        if (trimmed.Length == 0)
            throw new InvalidOperationException("Branch is required.");

        if (trimmed.Length > 255
            || trimmed.StartsWith('-')
            || trimmed.StartsWith('/')
            || trimmed.EndsWith('/')
            || trimmed.EndsWith(".lock", StringComparison.Ordinal)
            || trimmed.Contains("..", StringComparison.Ordinal)
            || trimmed.Contains("@{", StringComparison.Ordinal)
            || !BranchName().IsMatch(trimmed))
        {
            throw new InvalidOperationException("Branch must be a valid Git branch name.");
        }

        return trimmed;
    }

    private static string DeriveDirectoryName(string repository)
    {
        var withoutTrailingSlash = repository.TrimEnd('/');
        var lastSeparator = withoutTrailingSlash.LastIndexOfAny(['/', ':']);
        var candidate = lastSeparator >= 0 ? withoutTrailingSlash[(lastSeparator + 1)..] : withoutTrailingSlash;
        if (candidate.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            candidate = candidate[..^4];

        var sanitized = new string(candidate
            .Select(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-' ? character : '-')
            .ToArray())
            .Trim('-', '.');

        if (sanitized.Length == 0)
            throw new InvalidOperationException("Repository must end with a usable repository name.");

        return sanitized;
    }

    [GeneratedRegex(@"^(?:https?|ssh|git)://[^\s/]+/[^\s]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RemoteUrl();

    [GeneratedRegex(@"^[A-Za-z0-9._-]+@[A-Za-z0-9._-]+:[^\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex ScpLikeRemote();

    [GeneratedRegex(@"^[A-Za-z0-9._/-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex BranchName();
}

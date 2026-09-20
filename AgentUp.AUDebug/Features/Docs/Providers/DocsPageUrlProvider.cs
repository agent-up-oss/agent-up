using AgentUp.AUDebug.Features.Host.DTOs;

namespace AgentUp.AUDebug.Features.Docs.Providers;

public static class DocsPageUrlProvider
{
    private static readonly string[] AllowedRoots =
    [
        "/docs",
        "/developer-guide",
        "/design-system"
    ];

    public static string Resolve(string? pagePath)
    {
        var raw = string.IsNullOrWhiteSpace(pagePath) ? DebugLayout.DocsHomePath : pagePath.Trim();
        if (!IsSafeRaw(raw))
            throw new InvalidOperationException(PathError(pagePath));

        var hash = raw.IndexOf('#', StringComparison.Ordinal);
        var path = hash >= 0 ? raw[..hash] : raw;
        var fragment = hash >= 0 ? raw[hash..] : string.Empty;
        if (path.Length == 0)
            path = DebugLayout.DocsHomePath;
        if (!path.StartsWith('/'))
            path = NeedsDocsPrefix(path) ? $"{DebugLayout.DocsHomePath.TrimEnd('/')}/{path}" : $"/{path}";

        if (!HasAllowedRoot(path) || !IsSafePath(path) || (fragment.Length > 0 && !IsSafeFragment(fragment)))
            throw new InvalidOperationException(PathError(pagePath));

        return $"{DebugLayout.DocsUrl}{path}{fragment}";
    }

    private static bool NeedsDocsPrefix(string path)
        => !path.StartsWith("docs/", StringComparison.Ordinal)
           && path != "docs"
           && !path.StartsWith("developer-guide/", StringComparison.Ordinal)
           && path != "developer-guide"
           && !path.StartsWith("design-system", StringComparison.Ordinal);

    private static bool HasAllowedRoot(string path)
        => AllowedRoots.Any(root =>
            path.Equals(root, StringComparison.Ordinal)
            || path.StartsWith(root + "/", StringComparison.Ordinal));

    private static bool IsSafeRaw(string raw)
        => !raw.Contains('\\', StringComparison.Ordinal)
           && !raw.Contains("://", StringComparison.Ordinal)
           && !raw.Contains("..", StringComparison.Ordinal)
           && !raw.StartsWith("//", StringComparison.Ordinal)
           && !raw.Any(char.IsControl)
           && !raw.Any(char.IsWhiteSpace);

    private static bool IsSafePath(string path)
        => path.Length > 0
           && path.All(character => char.IsAsciiLetterOrDigit(character)
                                    || character is '/' or '_' or '-' or '.');

    private static bool IsSafeFragment(string fragment)
        => fragment.Length > 1
           && fragment[0] == '#'
           && fragment[1..].All(character => char.IsAsciiLetterOrDigit(character)
                                             || character is '_' or '-' or '.');

    private static string PathError(string? pagePath)
        => $"Error: docs page path '{pagePath}' is not a hosted docs, developer-guide, or design-system path.";
}

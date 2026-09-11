using System.Text;
using System.Text.RegularExpressions;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Matches repo-relative paths against the glob syntax used by verification path rules.
/// Deliberately small and dependency-free: "**" crosses directory separators, "*" and "?"
/// do not.
/// </summary>
public sealed class PathGlobProvider
{
    private readonly Dictionary<string, Regex> _compiled = new(StringComparer.Ordinal);

    /// <summary>
    /// Whether <paramref name="path"/> matches <paramref name="glob"/>. Both are compared
    /// with forward slashes and case sensitivity, matching how Git reports paths.
    /// </summary>
    public bool Matches(string glob, string path)
        => Pattern(glob).IsMatch(Normalize(path));

    public static string Normalize(string path)
        => path.Replace('\\', '/').TrimStart('.', '/');

    private Regex Pattern(string glob)
    {
        if (_compiled.TryGetValue(glob, out var existing))
            return existing;

        var compiled = new Regex(Translate(glob), RegexOptions.CultureInvariant);
        _compiled[glob] = compiled;
        return compiled;
    }

    private static string Translate(string glob)
    {
        var normalized = Normalize(glob);
        var pattern = new StringBuilder("^");

        for (var index = 0; index < normalized.Length; index++)
        {
            var current = normalized[index];
            var isDoubleStar = current == '*' && index + 1 < normalized.Length && normalized[index + 1] == '*';

            if (isDoubleStar)
            {
                var followedBySlash = index + 2 < normalized.Length && normalized[index + 2] == '/';
                if (followedBySlash)
                {
                    // "**/" matches zero or more leading directories, so "**/*.md"
                    // matches "README.md" as well as "docs/guide/README.md".
                    pattern.Append("(?:.*/)?");
                    index += 2;
                }
                else
                {
                    pattern.Append(".*");
                    index += 1;
                }

                continue;
            }

            pattern.Append(current switch
            {
                '*' => "[^/]*",
                '?' => "[^/]",
                _ => Regex.Escape(current.ToString())
            });
        }

        return pattern.Append('$').ToString();
    }
}

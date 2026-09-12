namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Parses the Git output the change source reads.
/// </summary>
/// <remarks>
/// Pure and separate from process execution so the parsing is covered by tests. Both
/// commands are invoked with -z, which NUL-terminates entries and disables the path
/// quoting that would otherwise mangle non-ASCII and whitespace filenames.
/// </remarks>
public sealed class GitChangeOutputParser
{
    /// <summary>
    /// Reads <c>git status --porcelain=v1 -z --no-renames</c>. Each entry is a two-column
    /// status code, a space, then the path — so the path starts at index 3 and the leading
    /// column must not be trimmed away first.
    /// </summary>
    public IReadOnlyList<string> ParseStatus(string output)
        => [.. Entries(output)
            .Where(entry => entry.Length > 3)
            .Select(entry => entry[3..].Trim())
            .Where(path => path.Length > 0)
            .Where(IsFilePath)];

    /// <summary>Reads <c>git diff --name-only -z</c>, which emits bare paths.</summary>
    public IReadOnlyList<string> ParseNames(string output)
        => [.. Entries(output).Select(entry => entry.Trim()).Where(path => path.Length > 0).Where(IsFilePath)];

    /// <summary>
    /// Rejects directory entries. Git collapses a wholly untracked directory into one
    /// trailing-slash entry unless --untracked-files=all is passed; a directory can never
    /// be hashed, so treating one as a file would silently record it as absent.
    /// </summary>
    private static bool IsFilePath(string path) => !path.EndsWith('/');

    private static IEnumerable<string> Entries(string output)
        => output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
}

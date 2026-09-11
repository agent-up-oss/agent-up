namespace AgentUp.Verification.Tests.Support;

/// <summary>
/// Builds a changed-file set — repo-relative path to content hash — without a test having
/// to spell out hash strings it does not care about.
/// </summary>
internal sealed class ChangeSetBuilder
{
    private readonly Dictionary<string, string> _files = new(StringComparer.Ordinal);

    public static ChangeSetBuilder Changing(params string[] paths)
    {
        var builder = new ChangeSetBuilder();
        return paths.Aggregate(builder, (current, path) => current.With(path));
    }

    public ChangeSetBuilder With(string path)
        => With(path, StableHashFor(path));

    public ChangeSetBuilder With(string path, string hash)
    {
        _files[path] = hash;
        return this;
    }

    /// <summary>Simulates the file being edited after a check ran.</summary>
    public ChangeSetBuilder Edited(string path)
        => With(path, StableHashFor(path) + "-edited");

    public ChangeSetBuilder Without(string path)
    {
        _files.Remove(path);
        return this;
    }

    public IReadOnlyDictionary<string, string> Build()
        => new Dictionary<string, string>(_files, StringComparer.Ordinal);

    /// <summary>
    /// A deterministic stand-in for a content hash: readable in failure messages and
    /// stable across runs, unlike a real digest of test fixture bytes.
    /// </summary>
    public static string StableHashFor(string path) => $"sha256:{path.GetHashCode(StringComparison.Ordinal):x8}";
}

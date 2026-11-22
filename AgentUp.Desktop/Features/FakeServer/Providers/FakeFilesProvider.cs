using System.Text;
using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.Models;

namespace AgentUp.Desktop.Features.FakeServer.Providers;

/// <summary>
/// The Demo's own files: the Harbor Shop repository the definition carries, what this workspace's
/// Git state says changed, and the edits made in this session. A file the agent added exists only
/// in its diff, so its text is read back from the added lines. Mobile's Demo answers the same way.
/// </summary>
public static class FakeFilesProvider
{
    public static FakeFilesResult Directory(
        string workspaceId,
        JsonObject? repository,
        IReadOnlyDictionary<string, string> edits,
        IReadOnlyList<FakeGitFile> changes,
        string? rawPath)
    {
        var path = Normalize(rawPath);
        if (path is null)
            return Invalid(rawPath);
        var files = Effective(repository, edits, changes);
        var prefix = path.Length == 0 ? "" : path + "/";
        if (path.Length > 0 && !files.Keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal)))
            return FakeFilesResult.Problem(404, "Not found", $"'{path}' is not a folder in this workspace.");

        var statuses = Statuses(changes);
        var under = files.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).Select(key => key[prefix.Length..]).ToList();
        var folders = under.Where(rest => rest.Contains('/'))
            .Select(rest => rest[..rest.IndexOf('/')])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => (JsonNode)new JsonObject
            {
                ["name"] = name,
                ["path"] = prefix + name,
                ["kind"] = "Directory",
                ["size"] = null,
                ["status"] = null,
                ["changedFiles"] = changes.Count(change => change.Status != "Deleted" && change.Path.StartsWith(prefix + name + "/", StringComparison.Ordinal)),
            });
        var leaves = under.Where(rest => !rest.Contains('/'))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => (JsonNode)new JsonObject
            {
                ["name"] = name,
                ["path"] = prefix + name,
                ["kind"] = "File",
                ["size"] = Encoding.UTF8.GetByteCount(files[prefix + name]),
                ["status"] = statuses.GetValueOrDefault(prefix + name),
                ["changedFiles"] = 0,
            });
        return FakeFilesResult.Ok(new JsonObject
        {
            ["workspaceId"] = workspaceId,
            ["path"] = path,
            ["entries"] = new JsonArray([.. folders, .. leaves]),
            ["truncated"] = false,
        });
    }

    public static FakeFilesResult File(
        JsonObject? repository,
        IReadOnlyDictionary<string, string> edits,
        IReadOnlyList<FakeGitFile> changes,
        string? rawPath)
    {
        var path = Normalize(rawPath);
        if (string.IsNullOrEmpty(path))
            return Invalid(rawPath);
        return Effective(repository, edits, changes).TryGetValue(path, out var text)
            ? FakeFilesResult.Ok(Describe(path, text, Statuses(changes).GetValueOrDefault(path)))
            : FakeFilesResult.Problem(404, "Not found", $"'{path}' is not a file in this workspace.");
    }

    /// <summary>
    /// Saves an edit when it was made from the text that is there now. The file then reads as
    /// modified in Git, with a diff of the whole file, so the Demo's Git tab shows the edit too.
    /// </summary>
    public static FakeFilesResult Write(
        JsonObject? repository,
        IDictionary<string, string> edits,
        IReadOnlyList<FakeGitFile> changes,
        string? path,
        string? content,
        string? version)
    {
        var current = File(repository, edits.AsReadOnly(), changes, path);
        if (current.Status != 200)
            return current;
        if (content is null || string.IsNullOrEmpty(version))
            return FakeFilesResult.Problem(400, "Invalid file request", "A save names the file, its new content, and the version it was edited from.");
        var name = current.Body["path"]!.GetValue<string>();
        if (!string.Equals(current.Body["version"]?.GetValue<string>(), version, StringComparison.Ordinal))
            return FakeFilesResult.Problem(409, "File changed on the Server", $"'{name}' changed on the Server since it was opened. Reload it to see the change.");

        edits[name] = content;
        var existing = changes.FirstOrDefault(change => change.Path == name);
        var change = existing is null
            ? new FakeGitFile(name, Path.GetFileName(name), "Modified", WholeFileDiff(name, repository?[name]?.GetValue<string>() ?? "", content))
            : null;
        return FakeFilesResult.Ok(Describe(name, content, existing?.Status ?? "Modified"), change);
    }

    /// <summary>Names exact text, so a save can prove it started from what is there now.</summary>
    public static string Version(string text)
    {
        var hash = 0x811c9dc5u;
        foreach (var character in text)
        {
            hash ^= character;
            hash *= 0x01000193u;
        }
        return $"demo-{hash:x8}-{text.Length}";
    }

    private static JsonObject Describe(string path, string text, string? status) => new()
    {
        ["path"] = path,
        ["size"] = Encoding.UTF8.GetByteCount(text),
        ["version"] = Version(text),
        ["status"] = status,
        ["isBinary"] = false,
        ["isTooLarge"] = false,
        ["content"] = text,
    };

    private static Dictionary<string, string> Effective(JsonObject? repository, IReadOnlyDictionary<string, string> edits, IReadOnlyList<FakeGitFile> changes)
    {
        var files = (repository ?? [])
            .Where(pair => pair.Value is JsonValue)
            .ToDictionary(pair => pair.Key, pair => pair.Value!.GetValue<string>(), StringComparer.Ordinal);
        foreach (var change in changes.Where(change => change.Status == "Deleted"))
            files.Remove(change.Path);
        foreach (var change in changes.Where(change => change.Status != "Deleted" && !files.ContainsKey(change.Path)))
            files[change.Path] = AddedText(change.Diff);
        foreach (var (path, text) in edits)
            files[path] = text;
        return files;
    }

    private static Dictionary<string, string> Statuses(IReadOnlyList<FakeGitFile> changes)
        => changes.Where(change => change.Status != "Deleted")
            .GroupBy(change => change.Path, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Status, StringComparer.Ordinal);

    private static string AddedText(string diff)
        => string.Concat(diff.Split('\n')
            .Where(line => line.StartsWith('+') && !line.StartsWith("+++", StringComparison.Ordinal))
            .Select(line => line[1..] + "\n"));

    private static string WholeFileDiff(string path, string before, string after)
    {
        static string[] Lines(string text) => text.Length == 0 ? [] : text.TrimEnd('\n').Split('\n');
        var old = Lines(before);
        var next = Lines(after);
        return string.Join('\n', new[] { $"--- a/{path}", $"+++ b/{path}", $"@@ -1,{old.Length} +1,{next.Length} @@" }
            .Concat(old.Select(line => "-" + line))
            .Concat(next.Select(line => "+" + line))
            .Append(""));
    }

    private static string? Normalize(string? path)
    {
        var segments = (path ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries).Where(segment => segment != ".").ToArray();
        return segments.Any(segment => segment == ".." || string.Equals(segment, ".git", StringComparison.OrdinalIgnoreCase))
            ? null
            : string.Join('/', segments);
    }

    private static FakeFilesResult Invalid(string? path)
        => FakeFilesResult.Problem(400, "Invalid file request", $"'{path}' is not a workspace path.");
}

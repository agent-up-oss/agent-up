using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.Models;

namespace AgentUp.Desktop.Features.FakeServer.Providers;

public static class FakeGitProvider
{
    public static JsonObject Head(JsonObject git)
    {
        var changes = ChangesObject(git);
        return new JsonObject
        {
            ["branch"] = changes["branch"]?.DeepClone() ?? "main",
            ["localBranches"] = changes["localBranches"]?.DeepClone() ?? new JsonArray("main"),
            ["remoteBranches"] = changes["remoteBranches"]?.DeepClone() ?? new JsonArray(new JsonObject { ["remote"] = "origin", ["name"] = "main" }),
            ["upstream"] = changes["upstream"]?.DeepClone() ?? "origin/main",
            ["ahead"] = changes["ahead"]?.DeepClone() ?? 0,
            ["behind"] = changes["behind"]?.DeepClone() ?? 0,
            ["commit"] = changes["commit"]?.DeepClone()
        };
    }

    public static JsonObject Commit(JsonObject git, IReadOnlyList<string> files, string message)
    {
        var selected = files.Where(path => FindFile(git, path) is not null).ToArray();
        if (selected.Length == 0)
            return Result(succeeded: false, "Select at least one file to commit.");

        var commit = NextCommit(git);
        RemoveFiles(git, selected);
        SetInt(ChangesObject(git), "ahead", IntValue(ChangesObject(git), "ahead") + 1);
        ChangesObject(git)["commit"] = commit[..Math.Min(7, commit.Length)];
        PrependLog(git, commit, message.Trim().Length == 0 ? "chore: update harbor shop" : message.Trim(), "Demo", ["HEAD", Branch(git)]);
        return Result(succeeded: true, commit: commit);
    }

    public static JsonObject Discard(JsonObject git, IReadOnlyList<string> files)
    {
        RemoveFiles(git, files);
        return Result(succeeded: true, head: Head(git));
    }

    public static JsonObject Fetch(JsonObject git)
    {
        if (IntValue(ChangesObject(git), "behind") == 0)
            SetInt(ChangesObject(git), "behind", 1);
        return Result(succeeded: true, head: Head(git));
    }

    public static JsonObject Pull(JsonObject git)
    {
        if (IntValue(ChangesObject(git), "behind") > 0)
        {
            var commit = NextCommit(git);
            SetInt(ChangesObject(git), "behind", 0);
            ChangesObject(git)["commit"] = commit[..Math.Min(7, commit.Length)];
            PrependLog(git, commit, "chore(storefront): restock the harbor mug", "origin", ["origin/main"]);
        }

        return Result(succeeded: true, head: Head(git));
    }

    public static JsonObject Push(JsonObject git)
    {
        SetInt(ChangesObject(git), "ahead", 0);
        return Result(succeeded: true, head: Head(git));
    }

    public static JsonObject SwitchBranch(JsonObject git, string name, bool create)
    {
        var branch = name.Trim();
        if (branch.Length == 0)
            return Result(succeeded: false, "A branch name is required.");
        var locals = LocalBranches(git);
        if (create && !locals.Contains(branch, StringComparer.Ordinal))
            locals.Add(branch);
        if (!locals.Contains(branch, StringComparer.Ordinal))
            return Result(succeeded: false, $"Branch {branch} does not exist.");
        WriteLocalBranches(git, locals);
        ChangesObject(git)["branch"] = branch;
        return Result(succeeded: true, head: Head(git));
    }

    public static JsonObject CheckoutRemote(JsonObject git, string name)
    {
        var raw = name.Trim();
        if (raw.Length == 0)
            return Result(succeeded: false, "A branch name is required.");
        var local = raw.Contains('/', StringComparison.Ordinal) ? raw[(raw.LastIndexOf('/') + 1)..] : raw;
        var locals = LocalBranches(git);
        if (!locals.Contains(local, StringComparer.Ordinal))
            locals.Add(local);
        WriteLocalBranches(git, locals);
        ChangesObject(git)["branch"] = local;
        return Result(succeeded: true, head: Head(git));
    }

    public static string AddAgentFile(JsonObject git)
    {
        var sequence = IntValue(git, "nextAgentFile");
        if (sequence == 0)
            sequence = 1;
        git["nextAgentFile"] = sequence + 1;
        var suffix = sequence == 1 ? "" : sequence.ToString();
        var path = $"apps/storefront/PromoBanner{suffix}.tsx";
        var diff = $"--- /dev/null\n+++ b/{path}\n@@ -0,0 +1,5 @@\n+export function PromoBanner{suffix}() {{\n+  return <aside>Weekly harbor special</aside>;\n+}}\n";
        UpsertFile(git, path, "Added", diff);
        return path;
    }

    public static JsonNode? Diff(JsonObject git, string path)
        => git["diffs"]?[path];

    private static void UpsertFile(JsonObject git, string path, string status, string diff)
    {
        var files = Flatten(git).Where(file => file.Path != path).ToList();
        files.Add(new FakeGitFile(path, Path.GetFileName(path), status, diff));
        WriteFiles(git, files);
        if (git["diffs"] is not JsonObject diffs)
        {
            diffs = [];
            git["diffs"] = diffs;
        }

        diffs[path] = new JsonObject
        {
            ["path"] = path,
            ["status"] = status,
            ["isBinary"] = false,
            ["diff"] = diff
        };
        var unassigned = Unassigned(git);
        if (!unassigned.Contains(path, StringComparer.Ordinal))
            unassigned.Add(path);
        WriteUnassigned(git, unassigned);
        ChangesObject(git)["fileCount"] = files.Count;
    }

    private static void RemoveFiles(JsonObject git, IEnumerable<string> paths)
    {
        var remove = paths.ToHashSet(StringComparer.Ordinal);
        var files = Flatten(git).Where(file => !remove.Contains(file.Path)).ToList();
        WriteFiles(git, files);
        if (git["diffs"] is JsonObject diffs)
        {
            foreach (var path in remove)
                diffs.Remove(path);
        }

        WriteUnassigned(git, Unassigned(git).Where(path => !remove.Contains(path)).ToList());
        ChangesObject(git)["fileCount"] = files.Count;
    }

    private static List<FakeGitFile> Flatten(JsonObject git)
    {
        var files = new List<FakeGitFile>();
        Collect(ChangesObject(git)["root"] as JsonObject, git["diffs"] as JsonObject, files);
        return files;
    }

    private static void Collect(JsonObject? directory, JsonObject? diffs, List<FakeGitFile> files)
    {
        if (directory is null)
            return;
        foreach (var child in (directory["directories"] as JsonArray ?? []).OfType<JsonObject>())
            Collect(child, diffs, files);
        files.AddRange((directory["files"] as JsonArray ?? [])
            .OfType<JsonObject>()
            .Select(file => (
                Path: file["path"]?.GetValue<string>(),
                Name: file["name"]?.GetValue<string>(),
                Status: file["status"]?.GetValue<string>() ?? "Modified"))
            .Where(file => !string.IsNullOrWhiteSpace(file.Path))
            .Select(file => new FakeGitFile(
                file.Path!,
                file.Name ?? Path.GetFileName(file.Path!),
                string.Equals(file.Status, "modified", StringComparison.OrdinalIgnoreCase) ? "Modified" : file.Status,
                diffs?[file.Path!]?["diff"]?.GetValue<string>() ?? $"--- a/{file.Path}\n+++ b/{file.Path}\n")));
    }

    private static void WriteFiles(JsonObject git, List<FakeGitFile> files)
    {
        var root = new JsonObject
        {
            ["name"] = "",
            ["path"] = "",
            ["directories"] = new JsonArray(),
            ["files"] = new JsonArray()
        };
        foreach (var file in files)
        {
            var parts = file.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var directory = root;
            for (var index = 0; index < parts.Length - 1; index++)
            {
                var path = string.Join('/', parts.Take(index + 1));
                var directories = (JsonArray)directory["directories"]!;
                var child = directories.OfType<JsonObject>()
                    .FirstOrDefault(item => item["path"]?.GetValue<string>() == path);
                if (child is null)
                {
                    child = new JsonObject
                    {
                        ["name"] = parts[index],
                        ["path"] = path,
                        ["directories"] = new JsonArray(),
                        ["files"] = new JsonArray()
                    };
                    directories.Add(child);
                }

                directory = child;
            }

            ((JsonArray)directory["files"]!).Add(new JsonObject
            {
                ["name"] = file.Name,
                ["path"] = file.Path,
                ["status"] = file.Status
            });
        }

        ChangesObject(git)["root"] = root;
    }

    private static FakeGitFile? FindFile(JsonObject git, string path)
        => Flatten(git).FirstOrDefault(file => file.Path == path);

    private static JsonObject ChangesObject(JsonObject git)
        => git["changes"] as JsonObject ?? throw new InvalidOperationException("The fake git node is missing changes.");

    private static string Branch(JsonObject git)
        => ChangesObject(git)["branch"]?.GetValue<string>() ?? "main";

    private static List<string> LocalBranches(JsonObject git)
        => (ChangesObject(git)["localBranches"] as JsonArray ?? [])
            .Select(item => item?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();

    private static void WriteLocalBranches(JsonObject git, IEnumerable<string> branches)
        => ChangesObject(git)["localBranches"] = new JsonArray(branches.Select(branch => JsonValue.Create(branch)).ToArray());

    private static List<string> Unassigned(JsonObject git)
        => (git["queue"]?["unassignedFiles"] as JsonArray ?? [])
            .Select(item => item?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Cast<string>()
            .ToList();

    private static void WriteUnassigned(JsonObject git, IEnumerable<string> files)
    {
        if (git["queue"] is not JsonObject queue)
        {
            queue = [];
            git["queue"] = queue;
        }

        queue["unassignedFiles"] = new JsonArray(files.Select(file => JsonValue.Create(file)).ToArray());
    }

    private static void PrependLog(JsonObject git, string commit, string subject, string author, string[] refs)
    {
        if (git["log"] is not JsonObject log)
        {
            log = new JsonObject { ["commits"] = new JsonArray() };
            git["log"] = log;
        }

        var commits = log["commits"] as JsonArray ?? [];
        var parents = new JsonArray();
        if (commits.Count > 0 && commits[0]?["id"]?.GetValue<string>() is { } parent)
            parents.Add(parent);
        foreach (var existing in commits.OfType<JsonObject>().Where(item => item["refs"] is JsonArray))
        {
            var current = (JsonArray)existing["refs"]!;
            existing["refs"] = new JsonArray(current.Where(item => item?.GetValue<string>() != "HEAD").Select(item => item!.DeepClone()).ToArray());
        }

        commits.Insert(0, new JsonObject
        {
            ["id"] = commit,
            ["shortId"] = commit[..Math.Min(7, commit.Length)],
            ["parents"] = parents,
            ["subject"] = subject,
            ["author"] = author,
            ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
            ["refs"] = new JsonArray(refs.Select(item => JsonValue.Create(item)).ToArray())
        });
        log["commits"] = commits;
    }

    private static string NextCommit(JsonObject git)
    {
        var next = IntValue(git, "nextCommit");
        if (next == 0)
            next = 1;
        git["nextCommit"] = next + 1;
        return $"f4ke{next:D8}";
    }

    private static int IntValue(JsonObject node, string name)
    {
        if (node[name] is not JsonValue json)
            return 0;
        if (json.TryGetValue<int>(out var number))
            return number;
        return json.TryGetValue<long>(out var longer) ? (int)longer : 0;
    }

    private static void SetInt(JsonObject node, string name, int value)
        => node[name] = value;

    private static JsonObject Result(bool succeeded, string? error = null, string? commit = null, JsonObject? head = null)
    {
        var payload = new JsonObject
        {
            ["found"] = true,
            ["succeeded"] = succeeded,
            ["error"] = error,
            ["commit"] = commit
        };
        if (head is not null)
            payload["head"] = head;
        return payload;
    }

}

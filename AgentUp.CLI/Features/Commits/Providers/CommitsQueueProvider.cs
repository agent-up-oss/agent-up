using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.CLI.Features.Commits.Interfaces;
using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Features.Commits.Providers;

public sealed class CommitsQueueProvider(ICommitsGitProvider git) : ICommitsQueueProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<CommitsQueue> ReadAsync(CancellationToken cancellationToken = default)
    {
        var path = await QueuePathAsync(cancellationToken);
        if (!File.Exists(path))
            return CommitsQueue.Empty();

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<QueueJson>(json, JsonOptions)?.ToModel()
               ?? CommitsQueue.Empty();
    }

    public async Task WriteAsync(CommitsQueue queue, CancellationToken cancellationToken = default)
    {
        var path = await QueuePathAsync(cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(QueueJson.FromModel(queue), JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        var path = await QueuePathAsync(cancellationToken);
        if (File.Exists(path))
            File.Delete(path);
    }

    private async Task<string> QueuePathAsync(CancellationToken cancellationToken)
    {
        var root = await git.GetRepoRootAsync(cancellationToken);
        var repoId = RepoId(root);
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Join(baseDir, "agentup", "commits", repoId, "queue.json");
    }

    private static string RepoId(string repoRoot)
    {
        var normalized = Path.GetFullPath(repoRoot).ToLowerInvariant();
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }

    // JSON serialization shapes — separate from domain models
    private sealed record QueueJson(int Version, List<EntryJson> Commits)
    {
        public CommitsQueue ToModel() => new(Version, Commits.Select(e => e.ToModel()).ToList());
        public static QueueJson FromModel(CommitsQueue q) => new(q.Version, q.Commits.Select(EntryJson.FromModel).ToList());
    }

    private sealed record EntryJson(string Slice, string Message, List<string> Files, List<string> Tests)
    {
        public CommitEntry ToModel() => new(Slice, Message, Files, Tests);
        public static EntryJson FromModel(CommitEntry e) => new(e.Slice, e.Message, [.. e.Files], [.. e.Tests]);
    }
}

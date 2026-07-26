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
        return JsonSerializer.Deserialize<CommitsQueueJson>(json, JsonOptions)?.ToModel()
               ?? CommitsQueue.Empty();
    }

    public async Task WriteAsync(CommitsQueue queue, CancellationToken cancellationToken = default)
    {
        var path = await QueuePathAsync(cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(CommitsQueueJson.FromModel(queue), JsonOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        var path = await QueuePathAsync(cancellationToken);
        if (File.Exists(path))
            File.Delete(path);
    }

    public async Task SavePatchAsync(string slice, string patch, CancellationToken cancellationToken = default)
    {
        if (patch.Length == 0)
            return;

        var queuePath = await QueuePathAsync(cancellationToken);
        var patchDir = Path.Join(Path.GetDirectoryName(queuePath)!, "patches");
        Directory.CreateDirectory(patchDir);
        var safeSlice = string.Concat(slice.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddTHHmmss");
        var patchPath = Path.Join(patchDir, $"{safeSlice}-{timestamp}.patch");
        await File.WriteAllTextAsync(patchPath, patch, cancellationToken);
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
}

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
        var tempPath = Path.Join(Path.GetDirectoryName(path)!, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, path, true);
    }

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        var path = await QueuePathAsync(cancellationToken);
        if (File.Exists(path))
            File.Delete(path);
    }

    public async Task SavePatchAsync(string patchKey, string patch, CancellationToken cancellationToken = default)
    {
        if (patch.Length == 0)
            return;

        var queuePath = await QueuePathAsync(cancellationToken);
        var patchDir = Path.Join(Path.GetDirectoryName(queuePath)!, "patches");
        Directory.CreateDirectory(patchDir);
        var patchPath = Path.Join(patchDir, $"{SafePatchKey(patchKey)}.patch");
        if (File.Exists(patchPath))
            throw new InvalidOperationException($"Commit queue patch '{patchKey}' already exists.");

        var content = patch.EndsWith('\n') ? patch : patch + "\n";
        await File.WriteAllTextAsync(patchPath, content, cancellationToken);
    }

    public async Task<string?> ReadPatchAsync(string patchKey, CancellationToken cancellationToken = default)
    {
        var queuePath = await QueuePathAsync(cancellationToken);
        var patchDir = Path.Join(Path.GetDirectoryName(queuePath)!, "patches");
        if (!Directory.Exists(patchDir))
            return null;

        var patchPath = Path.Join(patchDir, $"{SafePatchKey(patchKey)}.patch");
        return File.Exists(patchPath) ? await File.ReadAllTextAsync(patchPath, cancellationToken) : null;
    }

    public async Task<T> WithLockAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var queuePath = await QueuePathAsync(cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(queuePath)!);
        var lockPath = Path.Join(Path.GetDirectoryName(queuePath)!, "queue.lock");

        await using var stream = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        return await operation(cancellationToken);
    }

    private static string SafePatchKey(string patchKey)
        => string.Concat(patchKey.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));

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

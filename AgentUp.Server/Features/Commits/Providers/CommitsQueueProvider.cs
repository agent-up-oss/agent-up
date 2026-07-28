using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Commits.Interfaces;
using AgentUp.Server.Features.Commits.Models;

namespace AgentUp.Server.Features.Commits.Providers;

public sealed class CommitsQueueProvider(ICommitsGitProvider git, string? baseDirectory = null) : ICommitsQueueProvider
{
    private static readonly TimeSpan LockRetryDelay = TimeSpan.FromMilliseconds(25);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<CommitsQueue> ReadAsync(string worktreePath, CancellationToken cancellationToken = default)
    {
        var path = await QueuePathAsync(worktreePath, cancellationToken);
        if (!File.Exists(path))
            return CommitsQueue.Empty();

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<CommitsQueueJson>(json, JsonOptions)?.ToModel()
               ?? CommitsQueue.Empty();
    }

    public async Task WriteAsync(string worktreePath, CommitsQueue queue, CancellationToken cancellationToken = default)
    {
        var path = await QueuePathAsync(worktreePath, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var json = JsonSerializer.Serialize(CommitsQueueJson.FromModel(queue), JsonOptions);
        var tempPath = Path.Join(Path.GetDirectoryName(path)!, $"{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            File.Move(tempPath, path, true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    public async Task SavePatchAsync(string worktreePath, string patchKey, string patch, CancellationToken cancellationToken = default)
    {
        if (patch.Length == 0)
            return;

        var queuePath = await QueuePathAsync(worktreePath, cancellationToken);
        var patchDir = Path.Join(Path.GetDirectoryName(queuePath)!, "patches");
        Directory.CreateDirectory(patchDir);
        var patchPath = Path.Join(patchDir, $"{SafePatchKey(patchKey)}.patch");
        if (File.Exists(patchPath))
            throw new InvalidOperationException($"Commit queue patch '{patchKey}' already exists.");

        var content = patch.EndsWith('\n') ? patch : patch + "\n";
        await File.WriteAllTextAsync(patchPath, content, cancellationToken);
    }

    public async Task<string?> ReadPatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
    {
        var queuePath = await QueuePathAsync(worktreePath, cancellationToken);
        var patchDir = Path.Join(Path.GetDirectoryName(queuePath)!, "patches");
        if (!Directory.Exists(patchDir))
            return null;

        var patchPath = Path.Join(patchDir, $"{SafePatchKey(patchKey)}.patch");
        return File.Exists(patchPath) ? await File.ReadAllTextAsync(patchPath, cancellationToken) : null;
    }

    public async Task DeletePatchAsync(string worktreePath, string patchKey, CancellationToken cancellationToken = default)
    {
        var queuePath = await QueuePathAsync(worktreePath, cancellationToken);
        var patchPath = Path.Join(Path.GetDirectoryName(queuePath)!, "patches", $"{SafePatchKey(patchKey)}.patch");
        if (File.Exists(patchPath))
            File.Delete(patchPath);
    }

    public async Task<T> WithLockAsync<T>(string worktreePath, Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var queuePath = await QueuePathAsync(worktreePath, cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(queuePath)!);
        var lockPath = Path.Join(Path.GetDirectoryName(queuePath)!, "queue.lock");

        await using var stream = await OpenLockAsync(lockPath, cancellationToken);
        return await operation(cancellationToken);
    }

    private static async Task<FileStream> OpenLockAsync(string lockPath, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException) when (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(LockRetryDelay, cancellationToken);
            }
        }
    }

    private static string SafePatchKey(string patchKey)
        => string.Concat(patchKey.Select(c => char.IsLetterOrDigit(c) || c == '-' ? c : '_'));

    private async Task<string> QueuePathAsync(string worktreePath, CancellationToken cancellationToken)
    {
        var root = await git.GetRepoRootAsync(worktreePath, cancellationToken);
        var repoId = RepoId(root);
        var baseDir = baseDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        return Path.Join(baseDir, "agentup", "commits", repoId, "queue.json");
    }

    private static string RepoId(string repoRoot)
    {
        var normalized = Path.GetFullPath(repoRoot);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}

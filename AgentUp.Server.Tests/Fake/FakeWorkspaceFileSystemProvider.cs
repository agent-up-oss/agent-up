using AgentUp.Server.Features.Files.DTOs;
using AgentUp.Server.Features.Files.Interfaces;
using AgentUp.Server.Features.Files.Models;

namespace AgentUp.Server.Tests.Fake;

/// <summary>A workspace folder held in memory: folders are path prefixes, files are byte arrays.</summary>
internal sealed class FakeWorkspaceFileSystemProvider : IWorkspaceFileSystemProvider
{
    public Dictionary<string, byte[]> Files { get; } = new(StringComparer.Ordinal);

    public string? LastRoot { get; private set; }

    public int Writes { get; private set; }

    /// <summary>When set, a write fails the way a read-only or vanished file does.</summary>
    public IOException? WriteFailure { get; set; }

    public WorkspaceDiskListing? ListDirectory(string root, string path, int maxEntries)
    {
        LastRoot = root;
        Reject(path);
        var prefix = path.Length == 0 ? string.Empty : path + "/";
        if (path.Length > 0 && !Files.Keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal)))
            return null;
        var entries = Files.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(key => key[prefix.Length..])
            .Select(rest => rest.Split('/')[0])
            .Distinct(StringComparer.Ordinal)
            .Select(name => Files.TryGetValue(prefix + name, out var bytes)
                ? new WorkspaceDiskEntry(name, prefix + name, WorkspaceFileEntryKind.File, bytes.LongLength)
                : new WorkspaceDiskEntry(name, prefix + name, WorkspaceFileEntryKind.Directory, null))
            .OrderBy(entry => entry.Kind)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .ToList();
        return new WorkspaceDiskListing(path, entries.Take(maxEntries).ToList(), entries.Count > maxEntries);
    }

    public Task<WorkspaceDiskFile?> ReadFileAsync(string root, string path, long maxBytes, CancellationToken cancellationToken)
    {
        LastRoot = root;
        Reject(path);
        if (!Files.TryGetValue(path, out var bytes))
            return Task.FromResult<WorkspaceDiskFile?>(null);
        return Task.FromResult<WorkspaceDiskFile?>(bytes.LongLength > maxBytes
            ? new WorkspaceDiskFile(path, bytes.LongLength, null)
            : new WorkspaceDiskFile(path, bytes.LongLength, bytes));
    }

    public Task WriteFileAsync(string root, string path, byte[] bytes, CancellationToken cancellationToken)
    {
        if (WriteFailure is not null)
            throw WriteFailure;
        Writes++;
        Files[path] = bytes;
        return Task.CompletedTask;
    }

    private static void Reject(string path)
    {
        if (path.Contains("..", StringComparison.Ordinal))
            throw new WorkspaceFilePathException($"'{path}' must stay inside the workspace folder.");
    }
}

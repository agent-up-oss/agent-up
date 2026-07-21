using System.Security.Cryptography;
using System.Text;

namespace AgentUp.Server.Features.Processes.Repositories;

public sealed class FileOutputRepository : IOutputRepository
{
    private readonly string _outputRoot;

    public FileOutputRepository(string baseDir)
    {
        _outputRoot = Path.GetFullPath(Path.Join(baseDir, "output"));
    }

    public async Task AppendAsync(string workspaceId, string appName, string line, CancellationToken ct = default)
    {
        var path = GetPath(workspaceId, appName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.AppendAllTextAsync(path, line + Environment.NewLine, ct);
    }

    public async Task<IReadOnlyList<string>> GetAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        var path = GetPath(workspaceId, appName);
        if (!File.Exists(path))
            return [];
        return await File.ReadAllLinesAsync(path, ct);
    }

    public Task ClearAsync(string workspaceId, string appName, CancellationToken ct = default)
    {
        var path = GetPath(workspaceId, appName);
        if (File.Exists(path))
            File.Delete(path);
        return Task.CompletedTask;
    }

    private string GetPath(string workspaceId, string appName)
    {
        var fileName = GetLogFileName(workspaceId, appName);
        var path = Path.GetFullPath(Path.Join(_outputRoot, fileName));
        var root = _outputRoot.EndsWith(Path.DirectorySeparatorChar)
            ? _outputRoot
            : _outputRoot + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (!path.StartsWith(root, comparison))
            throw new InvalidOperationException("Output log path escaped the output root.");

        return path;
    }

    private static string GetLogFileName(string workspaceId, string appName)
    {
        if (string.IsNullOrWhiteSpace(workspaceId))
            throw new ArgumentException("Workspace id is required.", nameof(workspaceId));

        if (string.IsNullOrWhiteSpace(appName))
            throw new ArgumentException("Application name is required.", nameof(appName));

        var bytes = Encoding.UTF8.GetBytes(workspaceId + "\0" + appName);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() + ".log";
    }
}

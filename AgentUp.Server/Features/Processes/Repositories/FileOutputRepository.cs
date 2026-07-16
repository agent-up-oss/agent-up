using System.Globalization;

namespace AgentUp.Server.Features.Processes.Repositories;

public sealed class FileOutputRepository : IOutputRepository
{
    private readonly string _baseDir;

    public FileOutputRepository(string baseDir)
    {
        _baseDir = baseDir;
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
        EnsureValidPathComponent(workspaceId, nameof(workspaceId));
        EnsureValidPathComponent(appName, nameof(appName));

        var outputRoot = Path.GetFullPath(Path.Combine(_baseDir, "output"));
        var candidatePath = Path.GetFullPath(Path.Combine(outputRoot, workspaceId, $"{appName}.log"));
        var rootWithSeparator = outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!candidatePath.StartsWith(rootWithSeparator, StringComparison.Ordinal))
        {
            throw new ArgumentException("Invalid path input.");
        }

        return candidatePath;
    }

    private static void EnsureValidPathComponent(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains(Path.DirectorySeparatorChar) ||
            value.Contains(Path.AltDirectorySeparatorChar) ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException(
                string.Format(CultureInfo.InvariantCulture, "Invalid path component: {0}", parameterName),
                parameterName);
        }
    }
}

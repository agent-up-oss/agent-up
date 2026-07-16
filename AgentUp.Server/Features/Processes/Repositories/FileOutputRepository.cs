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

    private string GetPath(string workspaceId, string appName) =>
        Path.Join(_baseDir, "output", workspaceId, $"{appName}.log");
}

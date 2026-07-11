using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Workspaces.Repositories;

public sealed class JsonWorkspaceRepository : IWorkspaceRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;

    public JsonWorkspaceRepository(string filePath)
    {
        _filePath = filePath;
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
    }

    public async Task<IReadOnlyList<Workspace>> LoadAllAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<List<Workspace>>(stream, Options, cancellationToken) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task SaveAllAsync(IReadOnlyList<Workspace> workspaces, CancellationToken cancellationToken = default)
    {
        var tmpPath = _filePath + ".tmp";
        await using (var stream = File.Create(tmpPath))
            await JsonSerializer.SerializeAsync(stream, workspaces, Options, cancellationToken);
        File.Move(tmpPath, _filePath, overwrite: true);
    }
}

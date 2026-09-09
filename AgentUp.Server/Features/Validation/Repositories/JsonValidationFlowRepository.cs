using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Interfaces;
namespace AgentUp.Server.Features.Validation.Repositories;
public sealed class JsonValidationFlowRepository(string filePath) : IValidationFlowRepository
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
    public async Task<IReadOnlyList<ValidationFlow>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) return [];
        try { await using var stream = File.OpenRead(filePath); return await JsonSerializer.DeserializeAsync<List<ValidationFlow>>(stream, Options, cancellationToken) ?? []; }
        catch (JsonException) { return []; }
    }
    public async Task SaveAsync(IReadOnlyList<ValidationFlow> flows, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temporaryPath = filePath + ".tmp";
        await using (var stream = File.Create(temporaryPath)) await JsonSerializer.SerializeAsync(stream, flows, Options, cancellationToken);
        File.Move(temporaryPath, filePath, true);
    }
}

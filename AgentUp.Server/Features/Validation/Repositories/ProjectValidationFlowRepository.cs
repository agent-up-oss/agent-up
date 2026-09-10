using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Interfaces;
using AgentUp.Server.Features.Validation.Providers;

namespace AgentUp.Server.Features.Validation.Repositories;

public sealed class ProjectValidationFlowRepository(ValidationFlowPathProvider paths) : IValidationFlowRepository
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<IReadOnlyList<ValidationFlow>> LoadAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        var filePath = paths.GetFlowFilePath(workspaceId);
        if (filePath is null || !File.Exists(filePath))
            return [];

        try
        {
            await using var stream = File.OpenRead(filePath);
            var file = await JsonSerializer.DeserializeAsync<ValidationFlowFile>(stream, Options, cancellationToken);
            return (file?.Flows ?? [])
                .Select(flow => ToRuntimeFlow(workspaceId, flow))
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task SaveAsync(string workspaceId, IReadOnlyList<ValidationFlow> flows, CancellationToken cancellationToken = default)
    {
        var filePath = paths.GetFlowFilePath(workspaceId)
            ?? throw new InvalidOperationException("Validation flows must be stored inside the workspace project directory.");

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temporaryPath = filePath + ".tmp";
        var payload = new ValidationFlowFile(flows.Select(ToPersistedFlow).ToList());
        await using (var stream = File.Create(temporaryPath))
            await JsonSerializer.SerializeAsync(stream, payload, Options, cancellationToken);
        File.Move(temporaryPath, filePath, true);
    }

    internal static ValidationFlow ToRuntimeFlow(string workspaceId, PersistedValidationFlow flow) =>
        new(
            flow.Id,
            workspaceId,
            flow.Application,
            flow.Name,
            flow.Description,
            flow.InitialPath,
            flow.InitialExpectations,
            flow.Steps,
            flow.UpdatedAtUtc,
            flow.Version);

    internal static PersistedValidationFlow ToPersistedFlow(ValidationFlow flow) =>
        new(
            flow.Id,
            flow.Application,
            flow.Name,
            flow.Description,
            flow.InitialPath,
            flow.InitialExpectations,
            flow.Steps,
            flow.UpdatedAtUtc,
            flow.Version);
}

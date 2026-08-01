using System.Text.Json;
using AgentUp.Server.Features.Workspaces.Models;

namespace AgentUp.Server.Features.Workspaces.Providers;

public sealed class WorkspaceEventFrameProvider
{
    private static readonly JsonSerializerOptions EventJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public string Format(WorkspaceStateChangedEvent evt) =>
        $"data: {JsonSerializer.Serialize(evt, EventJsonOptions)}\n\n";
}

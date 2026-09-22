using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Server.Features.Capabilities.DTOs;

public sealed record EnabledCapabilitySetDto
{
    public string SchemaVersion { get; init; } = "1";
    public IReadOnlyList<CapabilityPackageRef> Modules { get; init; } = [];
}

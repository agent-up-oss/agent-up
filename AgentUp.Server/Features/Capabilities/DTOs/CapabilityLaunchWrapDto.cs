namespace AgentUp.Server.Features.Capabilities.DTOs;

public sealed record CapabilityLaunchWrapDto(string FileName, IReadOnlyList<string> Arguments);

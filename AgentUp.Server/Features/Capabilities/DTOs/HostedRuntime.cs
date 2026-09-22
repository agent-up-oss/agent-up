namespace AgentUp.Server.Features.Capabilities.DTOs;

public sealed record HostedRuntime(
    string FileName,
    IReadOnlyList<string> Arguments,
    CapabilityStatusDto Status);

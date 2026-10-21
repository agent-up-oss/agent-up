namespace AgentUp.CLI.Features.Capabilities.DTOs;

public sealed record CapabilityProcessResult(int ExitCode, string StandardOutput, string StandardError);

namespace AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Models;

public sealed record CapabilityCommandResult(int ExitCode, string Stdout, string Stderr);

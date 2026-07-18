namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityValidationMessage(
    string Code,
    string Message,
    CapabilityValidationSeverity Severity);

namespace AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

public sealed record CapabilityValidationResult(
    bool CanRun,
    IReadOnlyList<CapabilityValidationMessage> Messages)
{
    public static CapabilityValidationResult Success(params CapabilityValidationMessage[] messages) =>
        new(true, messages);

    public static CapabilityValidationResult Failure(params CapabilityValidationMessage[] messages) =>
        new(false, messages);
}

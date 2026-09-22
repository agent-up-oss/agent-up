namespace AgentUp.Registry.Features.Packages.DTOs;

public sealed record CapabilityPackageValidationResult(
    bool IsValid,
    IReadOnlyList<string> Messages)
{
    public static CapabilityPackageValidationResult Success() => new(true, []);

    public static CapabilityPackageValidationResult Failure(params string[] messages) => new(false, messages);
}

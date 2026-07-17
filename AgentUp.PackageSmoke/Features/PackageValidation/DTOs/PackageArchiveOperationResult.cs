namespace AgentUp.PackageSmoke.Features.PackageValidation.DTOs;

public sealed record PackageArchiveOperationResult(bool Succeeded, string? ErrorMessage = null)
{
    public static PackageArchiveOperationResult Success() => new(true);
    public static PackageArchiveOperationResult Failure(string errorMessage) => new(false, errorMessage);
}

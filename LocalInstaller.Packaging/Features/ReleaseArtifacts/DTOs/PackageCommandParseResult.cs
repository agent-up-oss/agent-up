namespace AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

public sealed record PackageCommandParseResult(
    bool Succeeded,
    PackageCommand? Command,
    string? ErrorMessage)
{
    public static PackageCommandParseResult Success(PackageCommand command)
        => new(true, command, null);

    public static PackageCommandParseResult Failure(string errorMessage)
        => new(false, null, errorMessage);
}

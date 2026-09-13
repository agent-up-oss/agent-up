namespace AgentUp.AUDebug.Features.Host.DTOs;

public sealed record CommandResultDto(int ExitCode, string Message, string? ArtifactPath = null)
{
    public static CommandResultDto Ok(string message, string? artifactPath = null)
        => new(0, message, artifactPath);

    public static CommandResultDto Fail(string message)
        => new(1, message);
}

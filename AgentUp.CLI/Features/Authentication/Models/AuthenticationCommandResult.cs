namespace AgentUp.CLI.Features.Authentication.Models;

public sealed record AuthenticationCommandResult(bool Succeeded, string Message, int ExitCode)
{
    public static AuthenticationCommandResult Success(string message) => new(true, message, 0);

    public static AuthenticationCommandResult Failure(string message, int exitCode = 1) => new(false, message, exitCode);
}

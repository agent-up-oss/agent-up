namespace AgentUp.CLI.Features.Capabilities.DTOs;

public sealed record CapabilitiesCommandResult(bool Succeeded, string Message, int ExitCode)
{
    public static CapabilitiesCommandResult Ok(string message) => new(true, message, 0);

    public static CapabilitiesCommandResult Fail(string message, int exitCode = 1) => new(false, message, exitCode);
}

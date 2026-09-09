using AgentUp.CLI.Features.Authentication.Models;

namespace AgentUp.CLI.Features.Authentication.Services;

public sealed class AuthenticationOutputService(TextWriter output)
{
    public int WriteResult(AuthenticationCommandResult result)
    {
        output.WriteLine(result.Message);
        return result.ExitCode;
    }
}

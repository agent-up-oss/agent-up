using AgentUp.Server.Features.Authentication.Models;

namespace AgentUp.Server.Features.Authentication.Providers;

public sealed class AuthenticationModeProvider
{
    public AuthenticationModeProvider(IConfiguration configuration)
    {
        Current = Resolve(configuration);
    }

    public AuthenticationMode Current { get; }

    private static AuthenticationMode Resolve(IConfiguration configuration)
    {
        if (string.Equals(configuration["AGENTUP_AUTH_DISABLED"], "true", StringComparison.OrdinalIgnoreCase))
            return AuthenticationMode.Disabled;

        var mode = configuration["AGENTUP_AUTH_MODE"];
        if (string.Equals(mode, "externalBearer", StringComparison.OrdinalIgnoreCase))
            return AuthenticationMode.ExternalBearer;
        if (string.Equals(mode, "disabled", StringComparison.OrdinalIgnoreCase))
            return AuthenticationMode.Disabled;

        return AuthenticationMode.LocalAdministrator;
    }
}

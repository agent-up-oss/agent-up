using AgentUp.Server.Features.Connection.DTOs;

namespace AgentUp.Server.Features.Connection.Providers;

public sealed class ConnectionMetadataProvider(IConfiguration configuration)
{
    public ConnectionMetadataDto Current()
    {
        var mode = ResolveMode();
        return new ConnectionMetadataDto(
            "1",
            string.IsNullOrWhiteSpace(configuration["AGENTUP_CONNECTION_ID"])
                ? "local"
                : configuration["AGENTUP_CONNECTION_ID"]!,
            "selfHosted",
            string.IsNullOrWhiteSpace(configuration["AGENTUP_CONNECTION_DISPLAY_NAME"])
                ? "Agent-Up Server"
                : configuration["AGENTUP_CONNECTION_DISPLAY_NAME"]!,
            new ConnectionAuthenticationDto(
                mode,
                Prompt(mode),
                IdentifierRequired: false),
            "serverScoped");
    }

    private string ResolveMode()
    {
        if (string.Equals(configuration["AGENTUP_AUTH_DISABLED"], "true", StringComparison.OrdinalIgnoreCase))
            return "disabled";

        var mode = configuration["AGENTUP_AUTH_MODE"];
        if (string.Equals(mode, "externalBearer", StringComparison.OrdinalIgnoreCase))
            return "externalBearer";
        if (string.Equals(mode, "disabled", StringComparison.OrdinalIgnoreCase))
            return "disabled";

        return "localAdministrator";
    }

    private static string Prompt(string mode)
        => mode switch
        {
            "localAdministrator" => "Enter the administrator password to continue.",
            "disabled" => "Authentication is not required for this Server.",
            _ => "Sign in with a credential issued for this Server."
        };
}

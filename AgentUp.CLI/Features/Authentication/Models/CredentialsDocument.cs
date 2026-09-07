namespace AgentUp.CLI.Features.Authentication.Models;

public sealed class CredentialsDocument
{
    public Dictionary<string, string> Servers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

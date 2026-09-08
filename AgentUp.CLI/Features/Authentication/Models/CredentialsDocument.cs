namespace AgentUp.CLI.Features.Authentication.Models;

public sealed class CredentialsDocument
{
    private Dictionary<string, string>? _servers;

    public Dictionary<string, string> Servers
    {
        get => _servers ??= new(StringComparer.OrdinalIgnoreCase);
        set => _servers = value ?? new(StringComparer.OrdinalIgnoreCase);
    }
}

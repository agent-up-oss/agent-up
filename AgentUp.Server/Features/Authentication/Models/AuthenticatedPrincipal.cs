namespace AgentUp.Server.Features.Authentication.Models;

public sealed class AuthenticatedPrincipal
{
    public AuthenticatedPrincipal(
        string subject,
        string? tenant,
        string? workspace,
        IReadOnlyList<string> permissions)
    {
        Subject = subject;
        Tenant = tenant;
        Workspace = workspace;
        Permissions = permissions;
    }

    public string Subject { get; }

    public string? Tenant { get; }

    public string? Workspace { get; }

    public IReadOnlyList<string> Permissions { get; }
}

namespace AgentUp.Installers.Features.Installation.Models;

public sealed record ProductComponent(string Id, string DisplayName, string Description = "")
{
    public static ProductComponent Desktop
        => new("desktop", "Desktop", "Human UI for Agent-Up workspaces.");

    public static ProductComponent Server
        => new("server", "Server", "Local runtime authority and API service.");

    public static ProductComponent Cli
        => new("cli", "CLI", "Terminal command wrapper for the local Server.");
}

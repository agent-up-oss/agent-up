namespace AgentUp.Installers.Features.Installation.Models;

public sealed partial record ProductManifest
{
    private static readonly ProductManifest AgentUpManifest = new("Agent-Up", "agent-up", "AGENTUP")
    {
        Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli]
    };

    public static ProductManifest AgentUp()
        => AgentUpManifest;
}

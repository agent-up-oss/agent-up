namespace AgentUp.Installers.Features.UbuntuInstallation.Models;

public sealed partial record UbuntuInstallerManifest
{
    public static UbuntuInstallerManifest AgentUp()
        => new("agent-up", "agent-up-server.service", "agent-up", "Agent-Up");
}

namespace AgentUp.Installers.Features.Installation.Models;

public enum InstallerStep
{
    Welcome,
    License,
    Prerequisites,
    Docker,
    Components,
    Location,
    ServerConfiguration,
    Payload,
    Summary,
    Progress,
    Completion
}

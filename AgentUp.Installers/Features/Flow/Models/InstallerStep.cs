namespace AgentUp.Installers.Features.Flow.Models;

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

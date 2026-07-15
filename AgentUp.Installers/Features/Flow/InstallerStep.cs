namespace AgentUp.Installers.Features.Flow;

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

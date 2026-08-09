namespace AgentUp.Installers.Features.NixOsInstallation.Interfaces;

public interface INixOsExecutableLookup
{
    string? Find(string executableName);
}

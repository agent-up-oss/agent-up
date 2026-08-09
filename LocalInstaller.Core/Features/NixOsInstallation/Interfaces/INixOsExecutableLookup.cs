namespace LocalInstaller.Core.Features.NixOsInstallation.Interfaces;

public interface INixOsExecutableLookup
{
    string? Find(string executableName);
}

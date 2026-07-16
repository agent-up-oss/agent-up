using AgentUp.Packaging.Features.WindowsPackages.Interfaces;
using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;
using AgentUp.Packaging.Features.ReleaseArtifacts.Providers;

namespace AgentUp.Packaging.Features.UbuntuPackages.Interfaces;

public interface IPackageWriter : ISymbolicLinkPackageFileSystem
{
}

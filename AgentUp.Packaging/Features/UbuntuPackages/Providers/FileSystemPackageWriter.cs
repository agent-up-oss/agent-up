using AgentUp.Packaging.Features.UbuntuPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Shared.Providers;

namespace AgentUp.Packaging.Features.UbuntuPackages.Providers;

public sealed class FileSystemPackageWriter : SymbolicLinkPackageFileSystem, IPackageWriter
{
}

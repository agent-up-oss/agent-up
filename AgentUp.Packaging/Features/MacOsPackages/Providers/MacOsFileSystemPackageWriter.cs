using AgentUp.Packaging.Features.MacOsPackages.Interfaces;
using AgentUp.Packaging.Shared.Interfaces;
using AgentUp.Packaging.Shared.Providers;

namespace AgentUp.Packaging.Features.MacOsPackages.Providers;

public sealed class MacOsFileSystemPackageWriter : UnixPackageFileSystem, IMacOsPackageWriter
{
}

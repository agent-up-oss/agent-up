using LocalInstaller.Packaging.Features.UbuntuPackages.Interfaces;
using LocalInstaller.Packaging.Shared.Providers;

namespace LocalInstaller.Packaging.Features.UbuntuPackages.Providers;

public sealed class FileSystemPackageWriter : SymbolicLinkPackageFileSystem, IPackageWriter
{
}

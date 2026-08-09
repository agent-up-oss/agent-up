using LocalInstaller.Packaging.Features.ReleaseArtifacts.DTOs;

namespace LocalInstaller.Packaging.Features.ReleaseArtifacts.Interfaces;

public interface IPackageCommandParser
{
    PackageCommandParseResult Parse(string[] args);
}

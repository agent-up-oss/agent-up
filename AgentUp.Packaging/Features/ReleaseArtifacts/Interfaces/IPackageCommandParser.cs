using AgentUp.Packaging.Features.ReleaseArtifacts.DTOs;

namespace AgentUp.Packaging.Features.ReleaseArtifacts.Interfaces;

public interface IPackageCommandParser
{
    PackageCommandParseResult Parse(string[] args);
}

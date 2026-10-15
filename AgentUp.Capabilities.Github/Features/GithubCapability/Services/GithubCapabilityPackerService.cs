using AgentUp.Capabilities.Github.Features.GithubCapability.Providers;

namespace AgentUp.Capabilities.Github.Features.GithubCapability.Services;

public sealed class GithubCapabilityPackerService(GithubCapabilityPacker packer, GithubCapabilityPackageWriter writer)
{
    public string Pack(string packageDirectory)
    {
        writer.Write(packageDirectory, packer.Manifest(), packer.DefaultNix());
        return packageDirectory;
    }
}

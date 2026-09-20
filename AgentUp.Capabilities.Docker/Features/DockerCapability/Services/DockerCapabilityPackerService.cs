using AgentUp.Capabilities.Docker.Features.DockerCapability.Providers;

namespace AgentUp.Capabilities.Docker.Features.DockerCapability.Services;

public sealed class DockerCapabilityPackerService(
    DockerCapabilityPacker packer,
    DockerCapabilityPackageWriter writer)
{
    public string Pack(string packageDirectory)
    {
        writer.Write(packageDirectory, packer.Manifest(), packer.DefaultNix());
        return packageDirectory;
    }
}

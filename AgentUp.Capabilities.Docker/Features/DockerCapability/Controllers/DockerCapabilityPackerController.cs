using AgentUp.Capabilities.Docker.Features.DockerCapability.Services;

namespace AgentUp.Capabilities.Docker.Features.DockerCapability.Controllers;

public sealed class DockerCapabilityPackerController(DockerCapabilityPackerService packer)
{
    public string Pack(string packageDirectory) => packer.Pack(packageDirectory);
}

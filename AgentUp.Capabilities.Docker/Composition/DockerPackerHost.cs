using AgentUp.Capabilities.Docker.Features.DockerCapability.Controllers;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Providers;
using AgentUp.Capabilities.Docker.Features.DockerCapability.Services;

namespace AgentUp.Capabilities.Docker.Composition;

public static class DockerPackerHost
{
    public static string Pack(string packageDirectory)
        => new DockerCapabilityPackerController(
            new DockerCapabilityPackerService(new DockerCapabilityPacker(), new DockerCapabilityPackageWriter()))
            .Pack(packageDirectory);
}

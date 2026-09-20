using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;

namespace AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Controllers;

public sealed class DotnetCapabilityPackerController(DotnetCapabilityPackerService packer)
{
    public string Pack(string packageDirectory) => packer.Pack(packageDirectory);
}

using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Services;

namespace AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Controllers;

public sealed class ForgejoCapabilityPackerController(ForgejoCapabilityPackerService packer)
{
    public string Pack(string packageDirectory) => packer.Pack(packageDirectory);
}

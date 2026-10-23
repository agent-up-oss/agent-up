using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Controllers;
using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Providers;
using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Services;

namespace AgentUp.Capabilities.Forgejo.Composition;

public static class ForgejoPackerHost
{
    public static string Pack(string packageDirectory)
        => new ForgejoCapabilityPackerController(
            new ForgejoCapabilityPackerService(new ForgejoCapabilityPacker(), new ForgejoCapabilityPackageWriter()))
            .Pack(packageDirectory);
}

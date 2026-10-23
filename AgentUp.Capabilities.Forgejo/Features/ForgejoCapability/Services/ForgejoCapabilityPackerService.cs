using AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Providers;

namespace AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Services;

public sealed class ForgejoCapabilityPackerService(ForgejoCapabilityPacker packer, ForgejoCapabilityPackageWriter writer)
{
    public string Pack(string packageDirectory)
    {
        writer.Write(packageDirectory, packer.Manifest(), packer.DefaultNix());
        return packageDirectory;
    }
}

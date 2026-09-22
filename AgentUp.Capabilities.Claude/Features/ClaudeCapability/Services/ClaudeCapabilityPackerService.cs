using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Providers;

namespace AgentUp.Capabilities.Claude.Features.ClaudeCapability.Services;

public sealed class ClaudeCapabilityPackerService(ClaudeCapabilityPacker packer, ClaudeCapabilityPackageWriter writer)
{
    public string Pack(string packageDirectory)
    {
        writer.Write(packageDirectory, packer.Manifest(), packer.DefaultNix());
        return packageDirectory;
    }
}

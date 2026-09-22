using AgentUp.Capabilities.Codex.Features.CodexCapability.Providers;

namespace AgentUp.Capabilities.Codex.Features.CodexCapability.Services;

public sealed class CodexCapabilityPackerService(CodexCapabilityPacker packer, CodexCapabilityPackageWriter writer)
{
    public string Pack(string packageDirectory)
    {
        writer.Write(packageDirectory, packer.Manifest(), packer.DefaultNix());
        return packageDirectory;
    }
}

using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Providers;

namespace AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;

public sealed class DotnetCapabilityPackerService(
    DotnetCapabilityPacker packer,
    DotnetCapabilityPackageWriter writer)
{
    public string Pack(string packageDirectory)
    {
        writer.Write(packageDirectory, packer.Manifest(), packer.DefaultNix());
        return packageDirectory;
    }
}

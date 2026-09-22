using AgentUp.Capabilities.Cursor.Features.CursorCapability.Providers;

namespace AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

public sealed class CursorCapabilityPackerService(CursorCapabilityPacker packer, CursorCapabilityPackageWriter writer)
{
    public string Pack(string packageDirectory)
    {
        writer.Write(packageDirectory, packer.Manifest(), packer.DefaultNix());
        return packageDirectory;
    }
}

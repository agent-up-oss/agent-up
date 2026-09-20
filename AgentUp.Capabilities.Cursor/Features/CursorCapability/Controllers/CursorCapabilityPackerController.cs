using AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

namespace AgentUp.Capabilities.Cursor.Features.CursorCapability.Controllers;

public sealed class CursorCapabilityPackerController(CursorCapabilityPackerService packer)
{
    public string Pack(string packageDirectory) => packer.Pack(packageDirectory);
}

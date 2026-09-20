using AgentUp.Capabilities.Cursor.Features.CursorCapability.Controllers;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Providers;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

namespace AgentUp.Capabilities.Cursor.Composition;

public static class CursorPackerHost
{
    public static string Pack(string packageDirectory)
        => new CursorCapabilityPackerController(
            new CursorCapabilityPackerService(new CursorCapabilityPacker(), new CursorCapabilityPackageWriter()))
            .Pack(packageDirectory);
}

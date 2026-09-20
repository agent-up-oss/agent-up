using AgentUp.Capabilities.Codex.Features.CodexCapability.Controllers;
using AgentUp.Capabilities.Codex.Features.CodexCapability.Providers;
using AgentUp.Capabilities.Codex.Features.CodexCapability.Services;

namespace AgentUp.Capabilities.Codex.Composition;

public static class CodexPackerHost
{
    public static string Pack(string packageDirectory)
        => new CodexCapabilityPackerController(
            new CodexCapabilityPackerService(new CodexCapabilityPacker(), new CodexCapabilityPackageWriter()))
            .Pack(packageDirectory);
}

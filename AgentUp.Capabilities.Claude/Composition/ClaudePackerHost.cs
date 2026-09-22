using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Controllers;
using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Providers;
using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Services;

namespace AgentUp.Capabilities.Claude.Composition;

public static class ClaudePackerHost
{
    public static string Pack(string packageDirectory)
        => new ClaudeCapabilityPackerController(
            new ClaudeCapabilityPackerService(new ClaudeCapabilityPacker(), new ClaudeCapabilityPackageWriter()))
            .Pack(packageDirectory);
}

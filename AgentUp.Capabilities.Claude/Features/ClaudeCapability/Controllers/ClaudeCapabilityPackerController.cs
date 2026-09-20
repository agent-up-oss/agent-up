using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Services;

namespace AgentUp.Capabilities.Claude.Features.ClaudeCapability.Controllers;

public sealed class ClaudeCapabilityPackerController(ClaudeCapabilityPackerService packer)
{
    public string Pack(string packageDirectory) => packer.Pack(packageDirectory);
}

using AgentUp.Capabilities.Codex.Features.CodexCapability.Services;

namespace AgentUp.Capabilities.Codex.Features.CodexCapability.Controllers;

public sealed class CodexCapabilityPackerController(CodexCapabilityPackerService packer)
{
    public string Pack(string packageDirectory) => packer.Pack(packageDirectory);
}

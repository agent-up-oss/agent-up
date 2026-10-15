using AgentUp.Capabilities.Github.Features.GithubCapability.Services;

namespace AgentUp.Capabilities.Github.Features.GithubCapability.Controllers;

public sealed class GithubCapabilityPackerController(GithubCapabilityPackerService packer)
{
    public string Pack(string packageDirectory) => packer.Pack(packageDirectory);
}

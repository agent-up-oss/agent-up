using AgentUp.Capabilities.Github.Features.GithubCapability.Controllers;
using AgentUp.Capabilities.Github.Features.GithubCapability.Providers;
using AgentUp.Capabilities.Github.Features.GithubCapability.Services;

namespace AgentUp.Capabilities.Github.Composition;

public static class GithubPackerHost
{
    public static string Pack(string packageDirectory)
        => new GithubCapabilityPackerController(
            new GithubCapabilityPackerService(new GithubCapabilityPacker(), new GithubCapabilityPackageWriter()))
            .Pack(packageDirectory);
}

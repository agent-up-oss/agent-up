using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Controllers;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Providers;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;

namespace AgentUp.Capabilities.Dotnet.Composition;

public static class DotnetPackerHost
{
    public static string Pack(string packageDirectory)
        => new DotnetCapabilityPackerController(
            new DotnetCapabilityPackerService(new DotnetCapabilityPacker(), new DotnetCapabilityPackageWriter()))
            .Pack(packageDirectory);
}

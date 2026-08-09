using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Tests.Support;

internal static class AgentUpTestManifests
{
    private const string ProductName = "Agent-Up";
    private const string ProductSlug = "agent-up";
    private const string EnvironmentPrefix = "AGENTUP";
    private const string WindowsUpgradeCode = "8f9076cc-95f1-4bd6-a087-1686e5e0d540";

    public static ProductManifest Product()
        => new(ProductName, ProductSlug, EnvironmentPrefix)
        {
            Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli],
            WindowsUpgradeCode = WindowsUpgradeCode
        };

    public static PayloadSelection BundledPayload(Version version)
        => PayloadSelection.Bundled(ProductName, version);
}

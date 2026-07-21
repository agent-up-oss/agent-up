using AgentUp.InstallerApp.Features.Capabilities.Factories;
using AgentUp.InstallerApp.Features.Capabilities.Services;

namespace AgentUp.InstallerApp.Features.Capabilities.Controllers;

public static class CapabilitiesController
{
    public static CapabilityDashboardService CreateDefault()
        => CapabilityDashboardServiceFactory.CreateDefault();

    public static CapabilityDashboardService CreateNixOs()
        => CapabilityDashboardServiceFactory.CreateNixOs();

    public static CapabilityDashboardService CreateNixOs(string cacheRoot)
        => CapabilityDashboardServiceFactory.CreateNixOs(cacheRoot);

    public static CapabilityDashboardService CreateFake()
        => CapabilityDashboardServiceFactory.CreateFake();

    public static CapabilityDashboardService CreateEmpty()
        => CapabilityDashboardServiceFactory.CreateEmpty();
}

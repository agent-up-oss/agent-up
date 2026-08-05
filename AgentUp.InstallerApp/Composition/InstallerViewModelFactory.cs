using AgentUp.InstallerApp.Features.Capabilities.Controllers;
using AgentUp.InstallerApp.Features.Capabilities.Factories;
using AgentUp.InstallerApp.Features.Installation.ViewModels;
using AgentUp.InstallerConfig;
using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.InstallerApp.Composition;

public static class InstallerViewModelFactory
{
    public static InstallerViewModel CreateDefault()
    {
        var version = InstallerVersion();
        var manifest = AgentUpManifest();
        IInstallerPlatformAdapter adapter = InstallerPlatformAdapterFactory.Create(
            manifest,
            AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable(AgentUpProduct.FakeInstallerVariable),
            Environment.GetEnvironmentVariable(AgentUpProduct.NixOsLookupOnlyVariable) == "1" || InstallerPlatformAdapterFactory.IsNixOsHost());
        var model = new InstallerViewModel(
            InstallerSession.CreateDefault(manifest, version, manifest.DefaultInstallRoot(), PayloadSelection.Bundled(manifest.ProductName, version)),
            adapter,
            new CapabilitiesController(adapter.SupportsInstallActions
                ? CapabilityDashboardServiceFactory.CreateDefault()
                : CapabilityDashboardServiceFactory.CreateNixOs()));
        return model;
    }

    public static InstallerViewModel CreateFakeForTests()
    {
        var version = new Version(0, 0, 0);
        var manifest = AgentUpManifest();
        var installRoot = manifest.DefaultInstallRoot();
        return new InstallerViewModel(
            InstallerSession.CreateDefault(manifest, version, installRoot, PayloadSelection.Bundled(manifest.ProductName, version)),
            InstallerPlatformAdapterFactory.CreateFake(installRoot + " dry run"),
            new CapabilitiesController(CapabilityDashboardServiceFactory.CreateFake()));
    }

    public static InstallerViewModel CreateFakeWithNoModules()
    {
        var version = new Version(0, 0, 0);
        var manifest = AgentUpManifest();
        var installRoot = manifest.DefaultInstallRoot();
        return new InstallerViewModel(
            InstallerSession.CreateDefault(manifest, version, installRoot, PayloadSelection.Bundled(manifest.ProductName, version)),
            InstallerPlatformAdapterFactory.CreateFake(installRoot + " dry run"),
            new CapabilitiesController(CapabilityDashboardServiceFactory.CreateEmpty()));
    }

    private static Version InstallerVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return v is null || v == new Version(0, 0, 0, 0) ? new Version(0, 0, 0) : new Version(v.Major, v.Minor, v.Build);
    }

    private static ProductManifest AgentUpManifest()
        => new(AgentUpProduct.Name, AgentUpProduct.Slug, AgentUpProduct.EnvironmentPrefix)
        {
            Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli],
            WindowsUpgradeCode = AgentUpProduct.WindowsUpgradeCode
        };
}

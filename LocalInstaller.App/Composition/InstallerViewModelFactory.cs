using AgentUp.InstallerApp.Features.Capabilities.Controllers;
using AgentUp.InstallerApp.Features.Capabilities.Factories;
using AgentUp.InstallerApp.Features.Installation.ViewModels;
using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.InstallerApp.Composition;

public static class InstallerViewModelFactory
{
    public static InstallerViewModel CreateDefault(InstallerProductRegistration? product = null)
    {
        var version = InstallerVersion();
        var registration = product ?? throw new InvalidOperationException("LocalInstaller.App requires AppComposition.ConfigureProduct(...) before creating the installer window.");
        var manifest = registration.Product;
        IInstallerPlatformAdapter adapter = InstallerPlatformAdapterFactory.Create(
            manifest,
            AppContext.BaseDirectory,
            registration.FakeInstaller(),
            registration.UseNixOsLookupOnlyMode());
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
        var manifest = TestManifest();
        var installRoot = manifest.DefaultInstallRoot();
        return new InstallerViewModel(
            InstallerSession.CreateDefault(manifest, version, installRoot, PayloadSelection.Bundled(manifest.ProductName, version)),
            InstallerPlatformAdapterFactory.CreateFake(installRoot + " dry run"),
            new CapabilitiesController(CapabilityDashboardServiceFactory.CreateFake()));
    }

    public static InstallerViewModel CreateFakeWithNoModules()
    {
        var version = new Version(0, 0, 0);
        var manifest = TestManifest();
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

    private static ProductManifest TestManifest()
        => new("LocalInstaller Test", "localinstaller-test", "LOCALINSTALLERTEST")
        {
            Components = [ProductComponent.Desktop, ProductComponent.Server, ProductComponent.Cli],
            WindowsUpgradeCode = "11111111-1111-4111-8111-111111111111"
        };

    private static string? FakeInstaller(this InstallerProductRegistration registration)
        => registration.FakeInstallerVariable is null
            ? null
            : Environment.GetEnvironmentVariable(registration.FakeInstallerVariable);

    private static bool UseNixOsLookupOnlyMode(this InstallerProductRegistration registration)
        => registration.NixOsLookupOnlyVariable is not null
           && Environment.GetEnvironmentVariable(registration.NixOsLookupOnlyVariable) == "1"
           || InstallerPlatformAdapterFactory.IsNixOsHost();
}

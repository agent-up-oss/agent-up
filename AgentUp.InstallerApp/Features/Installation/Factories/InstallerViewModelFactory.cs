using AgentUp.InstallerApp.Features.Capabilities.Controllers;
using AgentUp.InstallerApp.Features.Installation.ViewModels;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.InstallerApp.Features.Installation.Factories;

public static class InstallerViewModelFactory
{
    public static InstallerViewModel CreateDefault()
    {
        var version = InstallerVersion();
        var manifest = ProductManifest.AgentUp();
        IInstallerPlatformAdapter adapter = InstallerAdapterFactory.Create();
        var model = new InstallerViewModel(
            InstallerSession.CreateDefault(manifest, version, manifest.DefaultInstallRoot(), PayloadSelection.Bundled(version)),
            adapter,
            adapter.SupportsInstallActions ? CapabilitiesController.CreateDefault() : CapabilitiesController.CreateNixOs());
        InitializeViewModel(model);
        return model;
    }

    public static InstallerViewModel CreateFakeForTests()
    {
        var version = new Version(0, 0, 0);
        var manifest = ProductManifest.AgentUp();
        var installRoot = manifest.DefaultInstallRoot();
        var model = new InstallerViewModel(
            InstallerSession.CreateDefault(manifest, version, installRoot, PayloadSelection.Bundled(version)),
            InstallerPlatformAdapterFactory.CreateFake(installRoot + " dry run"),
            CapabilitiesController.CreateFake());
        InitializeViewModel(model);
        return model;
    }

    public static InstallerViewModel CreateFakeWithNoModules()
    {
        var version = new Version(0, 0, 0);
        var manifest = ProductManifest.AgentUp();
        var installRoot = manifest.DefaultInstallRoot();
        var model = new InstallerViewModel(
            InstallerSession.CreateDefault(manifest, version, installRoot, PayloadSelection.Bundled(version)),
            InstallerPlatformAdapterFactory.CreateFake(installRoot + " dry run"),
            CapabilitiesController.CreateEmpty());
        InitializeViewModel(model);
        return model;
    }

    private static void InitializeViewModel(InstallerViewModel model)
    {
        try
        {
            model.RefreshAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize InstallerViewModel", ex);
        }
    }

    private static Version InstallerVersion()
    {
        var v = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        return v is null || v == new Version(0, 0, 0, 0) ? new Version(0, 0, 0) : new Version(v.Major, v.Minor, v.Build);
    }
}

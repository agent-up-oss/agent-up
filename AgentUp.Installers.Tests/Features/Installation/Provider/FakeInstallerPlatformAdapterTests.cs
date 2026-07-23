using AgentUp.Installers.Features.Installation.Factories;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Interfaces;
using AgentUp.Installers.Features.UbuntuInstallation.Interfaces;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation.Providers;

namespace AgentUp.Installers.Tests.Features.Installation.Provider;

[TestFixture]
public class FakeInstallerPlatformAdapterTests
{
    [Test]
    public async Task ExecuteInstallAsync_reportsEveryPlannedOperationAndValidatesState()
    {
        var session = InstallerSession.CreateDefault(
            ProductManifest.AgentUp(),
            new Version(1, 2, 3),
            "/opt/agent-up",
            PayloadSelection.Bundled(new Version(1, 2, 3)));
        var adapter = new FakeInstallerPlatformAdapter();

        var plan = adapter.PlanInstall(session);
        var progress = new List<InstallProgress>();
        await foreach (var item in adapter.ExecuteInstallAsync(session))
            progress.Add(item);
        var report = await adapter.ValidateInstalledStateAsync(session);

        Assert.That(plan, Has.Count.EqualTo(8));
        Assert.That(plan.Any(operation => operation.RequiresElevation), Is.True);
        Assert.That(progress, Has.Count.EqualTo(plan.Count));
        Assert.That(progress.Last().CompletedOperations, Is.EqualTo(plan.Count));
        Assert.That(report.Succeeded, Is.True);
    }

    [Test]
    public async Task ExecuteComponentActionAsync_installsOnlyRequestedTargetStatus()
    {
        var session = InstallerSession.CreateDefault(
            ProductManifest.AgentUp(),
            new Version(1, 2, 3),
            "/opt/agent-up",
            PayloadSelection.Bundled(new Version(1, 2, 3)));
        var adapter = new FakeInstallerPlatformAdapter();

        var progress = new List<InstallProgress>();
        await foreach (var item in adapter.ExecuteComponentActionAsync(
                           ProductComponent.Cli,
                           InstallerComponentAction.Install,
                           session))
        {
            progress.Add(item);
        }

        var cli = await adapter.GetComponentStatusAsync(ProductComponent.Cli, session);
        var desktop = await adapter.GetComponentStatusAsync(ProductComponent.Desktop, session);
        Assert.That(progress, Has.Count.EqualTo(adapter.PlanComponentAction(ProductComponent.Cli, InstallerComponentAction.Install, session).Count));
        Assert.That(cli.Kind, Is.EqualTo(InstallerComponentStatusKind.Installed));
        Assert.That(desktop.Kind, Is.EqualTo(InstallerComponentStatusKind.NotInstalled));
    }

    [Test]
    public async Task ExecuteComponentActionAsync_uninstallReturnsTargetToNotInstalled()
    {
        var session = InstallerSession.CreateDefault(
            ProductManifest.AgentUp(),
            new Version(1, 2, 3),
            "/opt/agent-up",
            PayloadSelection.Bundled(new Version(1, 2, 3)));
        var adapter = new FakeInstallerPlatformAdapter();

        await adapter.ExecuteComponentActionAsync(ProductComponent.Server, InstallerComponentAction.Install, session).DrainAsync();
        await adapter.ExecuteComponentActionAsync(ProductComponent.Server, InstallerComponentAction.Uninstall, session).DrainAsync();

        var server = await adapter.GetComponentStatusAsync(ProductComponent.Server, session);
        Assert.That(server.Kind, Is.EqualTo(InstallerComponentStatusKind.NotInstalled));
    }
}

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
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Models;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Tests.Features.Installation;

[TestFixture]
public class FakeInstallerPlatformAdapterTests
{
    [Test]
    public async Task ExecuteInstallAsync_reportsEveryPlannedOperationAndValidatesState()
    {
        var session = InstallerSession.CreateDefault(
            "Agent-Up",
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
}

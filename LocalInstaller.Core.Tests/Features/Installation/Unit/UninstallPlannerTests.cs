using AgentUp.Installers.Composition;
using AgentUp.Installers.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.DTOs;
using AgentUp.Installers.Features.WindowsInstallation.Interfaces;
using AgentUp.Installers.Features.MacOsInstallation.Interfaces;
using AgentUp.Installers.Features.UbuntuInstallation.Interfaces;
using AgentUp.Installers.Features.Installation.Interfaces;
using AgentUp.Installers.Features.Installation;
using AgentUp.Installers.Features.Installation.Models;

namespace AgentUp.Installers.Tests.Features.Installation.Unit;

[TestFixture]
public class UninstallPlannerTests
{
    [Test]
    public void Create_preservesConfigurationAndData_forApplicationOnlyRemoval()
    {
        var plan = UninstallPlanner.Create(UninstallMode.ApplicationOnly);

        Assert.That(plan.RemoveBinaries, Is.True);
        Assert.That(plan.RemoveService, Is.True);
        Assert.That(plan.RemoveConfiguration, Is.False);
        Assert.That(plan.RemoveLocalData, Is.False);
    }

    [Test]
    public void Create_removesConfigurationButPreservesData_whenRequested()
    {
        var plan = UninstallPlanner.Create(UninstallMode.ApplicationAndConfiguration);

        Assert.That(plan.RemoveConfiguration, Is.True);
        Assert.That(plan.RemoveLocalData, Is.False);
    }

    [Test]
    public void Create_removesConfigurationAndData_whenExplicitlyRequested()
    {
        var plan = UninstallPlanner.Create(UninstallMode.ApplicationConfigurationAndLocalData);

        Assert.That(plan.RemoveConfiguration, Is.True);
        Assert.That(plan.RemoveLocalData, Is.True);
    }
}

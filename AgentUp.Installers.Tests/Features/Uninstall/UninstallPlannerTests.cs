using AgentUp.Installers.Features.Uninstall;

namespace AgentUp.Installers.Tests.Features.Uninstall;

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

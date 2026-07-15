using AgentUp.Installers.Features.Components;

namespace AgentUp.Installers.Tests.Features.Components;

[TestFixture]
public class ComponentSelectionTests
{
    [Test]
    public void CreateDefault_includesAllMachineLevelComponents()
    {
        var summary = ComponentSelection.CreateDefault("Agent-Up", new Version(1, 2, 3), "/opt/agent-up");

        Assert.That(summary.Includes(InstallerComponent.Server), Is.True);
        Assert.That(summary.Includes(InstallerComponent.Cli), Is.True);
        Assert.That(summary.Includes(InstallerComponent.Desktop), Is.True);
        Assert.That(summary.Includes(InstallerComponent.NativeService), Is.True);
        Assert.That(summary.Includes(InstallerComponent.RuntimeDependencies), Is.True);
        Assert.That(summary.Location.RootDirectory, Is.EqualTo("/opt/agent-up"));
    }
}

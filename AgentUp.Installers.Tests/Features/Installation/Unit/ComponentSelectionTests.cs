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

using AgentUp.Installers.Features.Installation.Factories;
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
public class PathPlannerTests
{
    [Test]
    public void Add_appendsManagedEntry_whenMissing()
    {
        var plan = PathPlanner.Add("/usr/bin:/bin", "/usr/local/bin", ':');

        Assert.That(plan.ToPathString(':'), Is.EqualTo("/usr/bin:/bin:/usr/local/bin"));
    }

    [Test]
    public void Add_doesNotDuplicateExistingEntry()
    {
        var plan = PathPlanner.Add("/usr/bin:/usr/local/bin", "/usr/local/bin/", ':');

        Assert.That(plan.Entries, Is.EqualTo(new[] { "/usr/bin", "/usr/local/bin" }));
    }

    [Test]
    public void Remove_removesOnlyManagedEntry()
    {
        var plan = PathPlanner.Remove("/usr/bin:/usr/local/bin:/bin", "/usr/local/bin", ':');

        Assert.That(plan.ToPathString(':'), Is.EqualTo("/usr/bin:/bin"));
    }
}

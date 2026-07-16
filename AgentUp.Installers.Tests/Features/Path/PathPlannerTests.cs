using AgentUp.Installers.Features.Path;
using AgentUp.Installers.Features.Path.Models;

namespace AgentUp.Installers.Tests.Features.Path;

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

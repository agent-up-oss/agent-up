using AgentUp.AUDebug.Features.Test.Providers;

namespace AgentUp.AUDebug.Tests.Features.Test.Provider;

[TestFixture]
public sealed class DebugTestSuiteCatalogTests
{
    private readonly DebugTestSuiteCatalog _catalog = new();

    [Test]
    public void All_returnsEverySuiteInStableOrder()
    {
        Assert.That(_catalog.TryResolve("all", out var suites, out var error), Is.True);
        Assert.That(error, Is.Null);
        Assert.That(suites.Select(suite => suite.Id), Is.EqualTo(_catalog.SuiteIds));
        Assert.That(
            _catalog.SuiteIds,
            Is.EqualTo(new[] { "design-system", "desktop", "mobile", "au-debug", "architecture" }));
    }

    [Test]
    public void ScopedId_returnsOnlyThatSuite()
    {
        Assert.That(_catalog.TryResolve("design-system", out var suites, out var error), Is.True);
        Assert.That(error, Is.Null);
        Assert.That(suites, Has.Count.EqualTo(1));
        Assert.That(suites[0].Id, Is.EqualTo("design-system"));
        Assert.That(suites[0].Steps, Has.Count.EqualTo(2));
        Assert.That(suites[0].Steps[0].Arguments, Is.EqualTo(new[] { "run", "build" }));
        Assert.That(suites[0].Steps[1].Arguments, Is.EqualTo(new[] { "test" }));
    }

    [Test]
    public void Mobile_runsTypecheckTestsAndWebExport()
    {
        Assert.That(_catalog.TryResolve("mobile", out var suites, out var error), Is.True);
        Assert.That(error, Is.Null);
        Assert.That(suites[0].Steps.Select(step => step.Arguments), Is.EqualTo(new[]
        {
            new[] { "run", "typecheck" },
            new[] { "test" },
            new[] { "run", "build:web" },
        }));
    }

    [Test]
    public void BuildAll_returnsDesignSystemThenMobile()
    {
        Assert.That(_catalog.TryResolveBuild("all", out var suites, out var error), Is.True);
        Assert.That(error, Is.Null);
        Assert.That(suites.Select(suite => suite.Id), Is.EqualTo(_catalog.BuildIds));
        Assert.That(_catalog.BuildIds, Is.EqualTo(new[] { "design-system", "mobile" }));
    }

    [Test]
    public void BuildScopedId_returnsOnlyThatTarget()
    {
        Assert.That(_catalog.TryResolveBuild("design-system", out var suites, out var error), Is.True);
        Assert.That(error, Is.Null);
        Assert.That(suites, Has.Count.EqualTo(1));
        Assert.That(suites[0].Steps, Has.Count.EqualTo(1));
        Assert.That(suites[0].Steps[0].Arguments, Is.EqualTo(new[] { "run", "build" }));
    }

    [Test]
    public void UnknownBuildId_listsKnownTargets()
    {
        Assert.That(_catalog.TryResolveBuild("desktop", out _, out var error), Is.False);
        Assert.That(error, Does.Contain("unknown build target"));
        Assert.That(error, Does.Contain("design-system"));
        Assert.That(error, Does.Contain("mobile"));
        Assert.That(error, Does.Not.Contain("architecture"));
    }

    [Test]
    public void UnknownId_listsKnownSuites()
    {
        Assert.That(_catalog.TryResolve("packaging", out _, out var error), Is.False);
        Assert.That(error, Does.Contain("unknown test suite"));
        Assert.That(error, Does.Contain("design-system"));
        Assert.That(error, Does.Contain("architecture"));
    }
}

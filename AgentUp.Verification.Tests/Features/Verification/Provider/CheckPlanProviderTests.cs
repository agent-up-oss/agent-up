using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Tests.Fake;
using AgentUp.Verification.Tests.Support;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Provider;

[TestFixture]
public sealed class CheckPlanProviderTests
{
    private static CheckPlanProvider ProviderOn(string platformId, bool isContinuousIntegration = false)
        => new(new PathGlobProvider(), new FakePlatformCapabilityProvider(platformId, isContinuousIntegration));

    [Test]
    public void CreatePlan_requiresTheCheckAPathRuleNames()
    {
        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            VerificationDomain.Configuration().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build());

        Assert.That(plan.Checks.Select(check => check.CheckId),
            Is.EquivalentTo(new[] { VerificationDomain.ArchitectureCheck, VerificationDomain.ServerUnitCheck }));
    }

    [Test]
    public void CreatePlan_requiresTheServerCheckWhenOnlyItsTestProjectChanged()
    {
        // The gap this whole module exists to close: a test-only change used to resolve to
        // nothing, so the tests an agent had just written were never executed.
        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            VerificationDomain.Configuration().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.ServerTest).Build());

        Assert.That(plan.Checks.Select(check => check.CheckId), Does.Contain(VerificationDomain.ServerUnitCheck));
    }

    [Test]
    public void CreatePlan_alwaysChecksAreRequiredForAnyChangeAtAll()
    {
        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            VerificationDomain.Configuration().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.DocumentationSource).Build());

        Assert.That(plan.Checks.Select(check => check.CheckId), Is.EqualTo(new[] { VerificationDomain.ArchitectureCheck }));
    }

    [Test]
    public void CreatePlan_returnsNothingWhenNoFilesChanged()
    {
        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            VerificationDomain.Configuration().Build(),
            new ChangeSetBuilder().Build());

        Assert.Multiple(() =>
        {
            Assert.That(plan.HasWork, Is.False);
            Assert.That(plan.UnmatchedFiles, Is.Empty);
        });
    }

    [Test]
    public void CreatePlan_reportsFilesNoRuleMatchedInsteadOfPassingSilently()
    {
        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            VerificationDomain.Configuration().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.UnmappedSource).Build());

        Assert.That(plan.UnmatchedFiles, Is.EqualTo(new[] { VerificationDomain.UnmappedSource }));
    }

    [Test]
    public void CreatePlan_treatsAnEmptyChecksListAsAnExplicitOptOut()
    {
        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            VerificationDomain.Configuration().WithoutAlways().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.DocumentationSource).Build());

        Assert.Multiple(() =>
        {
            Assert.That(plan.Checks, Is.Empty);
            Assert.That(plan.UnmatchedFiles, Is.Empty, "docs/** matched a rule, so it is covered, not unmapped.");
        });
    }

    [Test]
    public void CreatePlan_coversDependencyInputsSoAnUpstreamEditStalesTheDownstreamCheck()
    {
        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            VerificationDomain.Configuration().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.SharedPolicySource).Build());

        var serverUnit = plan.Checks.Single(check => check.CheckId == VerificationDomain.ServerUnitCheck);

        Assert.That(serverUnit.Covered.Keys, Does.Contain(VerificationDomain.SharedPolicySource));
    }

    [Test]
    public void CreatePlan_coversEveryChangedFileForAnAlwaysCheck()
    {
        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            VerificationDomain.Configuration().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.ServerSource, VerificationDomain.MobileSource).Build());

        var architecture = plan.Checks.Single(check => check.CheckId == VerificationDomain.ArchitectureCheck);

        Assert.That(architecture.Covered.Keys,
            Is.EquivalentTo(new[] { VerificationDomain.ServerSource, VerificationDomain.MobileSource }));
    }

    [Test]
    public void CreatePlan_doesNotCoverUnrelatedFilesInAScopedCheck()
    {
        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            VerificationDomain.Configuration().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.ServerSource, VerificationDomain.MobileSource).Build());

        var serverUnit = plan.Checks.Single(check => check.CheckId == VerificationDomain.ServerUnitCheck);

        Assert.That(serverUnit.Covered.Keys, Does.Not.Contain(VerificationDomain.MobileSource),
            "Editing Mobile must not invalidate the Server receipt.");
    }

    [Test]
    public void CreatePlan_skipsAPlatformCheckThatCannotRunHereWithoutSatisfyingIt()
    {
        var plan = ProviderOn(VerificationDomain.Linux, isContinuousIntegration: true).CreatePlan(
            VerificationDomain.Configuration().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build());

        var macOsSmoke = plan.Checks.Single(check => check.CheckId == VerificationDomain.MacOsSmokeCheck);

        Assert.Multiple(() =>
        {
            Assert.That(macOsSmoke.SkipReason, Is.EqualTo(CheckSkipReason.PlatformMismatch));
            Assert.That(macOsSmoke.Runnable, Is.False);
        });
    }

    [Test]
    public void CreatePlan_runsThePlatformCheckThatMatchesThisMachine()
    {
        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            VerificationDomain.Configuration().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build());

        var linuxSmoke = plan.Checks.Single(check => check.CheckId == VerificationDomain.LinuxSmokeCheck);

        Assert.That(linuxSmoke.Runnable, Is.True);
    }

    [Test]
    public void CreatePlan_skipsCiOnlyChecksOnADeveloperMachine()
    {
        var plan = ProviderOn(VerificationDomain.MacOs).CreatePlan(
            VerificationDomain.Configuration().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build());

        var macOsSmoke = plan.Checks.Single(check => check.CheckId == VerificationDomain.MacOsSmokeCheck);

        Assert.That(macOsSmoke.SkipReason, Is.EqualTo(CheckSkipReason.CiOnly),
            "macOS smoke is runnable on macOS but is declared ciOnly, so it must not block a local run.");
    }

    [Test]
    public void CreatePlan_recordsWhichRuleRequiredEachCheck()
    {
        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            VerificationDomain.Configuration().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.MobileSource).Build());

        var mobile = plan.Checks.Single(check => check.CheckId == VerificationDomain.MobileCheck);

        Assert.That(mobile.SelectedBy, Is.EqualTo(new[] { "AgentUp.Mobile/**" }));
    }

    [Test]
    public void CreatePlan_unionsChecksWhenSeveralRulesMatchTheSameChange()
    {
        var plan = ProviderOn(VerificationDomain.Linux, isContinuousIntegration: true).CreatePlan(
            VerificationDomain.Configuration().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.PackagingSource).Build());

        Assert.That(plan.Checks.Select(check => check.CheckId), Is.EquivalentTo(new[]
        {
            VerificationDomain.ArchitectureCheck,
            VerificationDomain.LinuxSmokeCheck,
            VerificationDomain.MacOsSmokeCheck
        }));
    }

    [Test]
    public void CreatePlan_returnsNothingWhenTheRepositoryHasNoVerificationSection()
    {
        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            VerificationConfiguration.Empty,
            ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build());

        Assert.That(plan.HasWork, Is.False);
    }

    [Test]
    public void CreatePlan_putsAlwaysChecksFirstSoFoundationalOnesRunBeforeSuitesThatAssumeThem()
    {
        var configuration = VerificationDomain.Configuration()
            .WithCheck(new CheckBuilder("zz-build").WithCommand("dotnet build"))
            .WithoutAlways()
            .WithAlways("zz-build", VerificationDomain.ArchitectureCheck)
            .Build();

        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            configuration,
            ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build());

        Assert.That(plan.Checks.Select(check => check.CheckId),
            Is.EqualTo(new[] { "zz-build", VerificationDomain.ArchitectureCheck, VerificationDomain.ServerUnitCheck }),
            "Declared 'always' order wins over alphabetical order.");
    }

    [Test]
    public void CreatePlan_ordersSelectedChecksByIdSoPlansAreComparable()
    {
        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            VerificationDomain.Configuration().WithoutAlways().Build(),
            ChangeSetBuilder.Changing(VerificationDomain.ServerSource, VerificationDomain.MobileSource).Build());

        Assert.That(plan.Checks.Select(check => check.CheckId), Is.Ordered);
    }

    [Test]
    public void CreatePlan_ordersSelectedChecksByDeclaredOrderBeforeId()
    {
        // patch-coverage consumes the reports the suites write, so it has to run after
        // them even though "patch-coverage" sorts before "server" alphabetically.
        var configuration = VerificationDomain.Configuration()
            .WithCheck(new CheckBuilder("patch-coverage").WithCommand("verify coverage").WithOrder(100))
            .WithPathRule("AgentUp.Server/**", VerificationDomain.ServerUnitCheck, "patch-coverage")
            .WithoutAlways()
            .Build();

        var plan = ProviderOn(VerificationDomain.Linux).CreatePlan(
            configuration,
            ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build());

        Assert.That(plan.Checks.Select(check => check.CheckId),
            Is.EqualTo(new[] { VerificationDomain.ServerUnitCheck, "patch-coverage" }));
    }
}

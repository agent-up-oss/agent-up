using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Features.Verification.Services;
using AgentUp.Verification.Tests.Fake;
using AgentUp.Verification.Tests.Support;

namespace AgentUp.Verification.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class VerificationPlanServiceTests
{
    private const string Root = "/repo";

    [Test]
    public async Task CreatePlanAsync_mergesEveryRegisteredChangeSource()
    {
        // The commit queue restores tracked files out of the working tree, so a second
        // source has to be able to contribute content Git can no longer see.
        var service = new VerificationPlanService(
            new StubConfigurationLoader(VerificationDomain.Configuration().Build()),
            new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider(VerificationDomain.Linux)),
            [
                new StaticChangedContentSource("git", ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build()),
                new StaticChangedContentSource("queue", ChangeSetBuilder.Changing(VerificationDomain.MobileSource).Build())
            ]);

        var plan = await service.CreatePlanAsync(Root);

        Assert.That(plan.ChangedFiles.Keys,
            Is.EquivalentTo(new[] { VerificationDomain.ServerSource, VerificationDomain.MobileSource }));
    }

    [Test]
    public async Task CreatePlanAsync_prefersTheFirstRegisteredSourceForAPathBothReport()
    {
        var service = new VerificationPlanService(
            new StubConfigurationLoader(VerificationDomain.Configuration().Build()),
            new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider(VerificationDomain.Linux)),
            [
                new StaticChangedContentSource("git", new ChangeSetBuilder().With(VerificationDomain.ServerSource, "sha256:working-tree").Build()),
                new StaticChangedContentSource("queue", new ChangeSetBuilder().With(VerificationDomain.ServerSource, "sha256:queued").Build())
            ]);

        var plan = await service.CreatePlanAsync(Root);

        Assert.That(plan.ChangedFiles[VerificationDomain.ServerSource], Is.EqualTo("sha256:working-tree"));
    }

    [Test]
    public async Task CreatePlanAsync_returnsNothingWhenNoSourceReportsAChange()
    {
        var service = new VerificationPlanService(
            new StubConfigurationLoader(VerificationDomain.Configuration().Build()),
            new CheckPlanProvider(new PathGlobProvider(), new FakePlatformCapabilityProvider(VerificationDomain.Linux)),
            [new StaticChangedContentSource("git", new ChangeSetBuilder().Build())]);

        Assert.That((await service.CreatePlanAsync(Root)).HasWork, Is.False);
    }
}

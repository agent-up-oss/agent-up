using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Features.Coverage.Services;
using AgentUp.Verification.Shared.Providers;
using AgentUp.Verification.Tests.Fake;
using AgentUp.Verification.Tests.Support;

namespace AgentUp.Verification.Tests.Features.Coverage.Unit;

[TestFixture]
public sealed class SliceCoverageServiceTests
{
    private const string Root = "/repo";
    private const string GitSlice = "AgentUp.Server/Features/Git";
    private const string CommitsSlice = "AgentUp.CLI/Features/Commits";

    /// <summary>
    /// Assembles the service over an explicit world. Each test declares its own inputs
    /// here rather than inheriting a shared fixture.
    /// </summary>
    private static SliceCoverageService ServiceOver(
        CoverageConfiguration configuration,
        params FileCoverage[] coverage)
        => new(
            new StubCoverageConfigurationLoader(configuration),
            new StaticCoverageReportReader(CoverageReport.FromFiles(coverage)),
            new PathGlobProvider());

    [Test]
    public async Task MeasureAsync_scoresASliceFromItsCoveredLines()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().WithSliceMinimum(70d).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1, 2, 3).Uncovered(4).Build());

        var result = await service.MeasureAsync(Root);

        Assert.That(result.Slices.Single().Percent, Is.EqualTo(75d));
    }

    [Test]
    public async Task MeasureAsync_namesTheSliceByProjectAndSliceFolder()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().WithSliceMinimum(70d).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1).Build());

        Assert.That((await service.MeasureAsync(Root)).Slices.Single().Slice, Is.EqualTo(GitSlice));
    }

    [Test]
    public async Task MeasureAsync_totalsEveryFileAndTypeFolderInTheSameSlice()
    {
        // The slice, not the type folder, is the unit: a thin Controllers folder is not
        // hidden by a well covered Services folder, but they are reported together.
        var service = ServiceOver(
            CoverageDomain.Configuration().WithSliceMinimum(70d).Build(),
            new FileCoverageBuilder("AgentUp.Server/Features/Git/Services/GitService.cs")
                .Covered(1, 2).Build(),
            new FileCoverageBuilder("AgentUp.Server/Features/Git/Controllers/GitController.cs")
                .Uncovered(1, 2).Build());

        var slice = (await service.MeasureAsync(Root)).Slices.Single();

        Assert.Multiple(() =>
        {
            Assert.That(slice.Slice, Is.EqualTo(GitSlice));
            Assert.That(slice.Covered, Is.EqualTo(2));
            Assert.That(slice.Coverable, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task MeasureAsync_reportsTheWorstSliceFirst()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().WithSliceMinimum(70d).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1, 2, 3, 4).Build(),
            new FileCoverageBuilder(CoverageDomain.CliSource).Uncovered(1, 2, 3, 4).Build());

        var result = await service.MeasureAsync(Root);

        Assert.That(result.Slices.Select(slice => slice.Slice),
            Is.EqualTo(new[] { CommitsSlice, GitSlice }));
    }

    [Test]
    public async Task MeasureAsync_failsASliceBelowTheFloor()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().WithSliceMinimum(70d).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1).Uncovered(2).Build());

        var result = await service.MeasureAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSatisfied, Is.False);
            Assert.That(result.Failing.Single().Slice, Is.EqualTo(GitSlice));
        });
    }

    [Test]
    public async Task MeasureAsync_acceptsASliceExactlyAtTheFloor()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().WithSliceMinimum(50d).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1).Uncovered(2).Build());

        Assert.That((await service.MeasureAsync(Root)).IsSatisfied, Is.True);
    }

    [Test]
    public async Task MeasureAsync_letsAnExemptionCarryASliceBelowTheFloor()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().WithSliceMinimum(70d).WithSliceExemptions(GitSlice).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1).Uncovered(2).Build());

        var result = await service.MeasureAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSatisfied, Is.True);
            Assert.That(result.Failing, Is.Empty);
        });
    }

    [Test]
    public async Task MeasureAsync_failsOnAnExemptionThatIsNoLongerNeeded()
    {
        // Otherwise the list outlives the debt it records and the floor silently stops
        // applying to a slice that has since been covered.
        var service = ServiceOver(
            CoverageDomain.Configuration().WithSliceMinimum(70d).WithSliceExemptions(GitSlice).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1, 2).Build());

        var result = await service.MeasureAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSatisfied, Is.False);
            Assert.That(result.ResolvedExemptions, Is.EqualTo(new[] { GitSlice }));
        });
    }

    [Test]
    public async Task MeasureAsync_ignoresAnExemptionForASliceNoReportMentions()
    {
        // A slice that is not measured is neither failing nor resolved: reporting it as
        // resolved would demand deleting an entry that is still protecting something.
        var service = ServiceOver(
            CoverageDomain.Configuration().WithSliceMinimum(70d).WithSliceExemptions(CommitsSlice).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1, 2).Build());

        var result = await service.MeasureAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSatisfied, Is.True);
            Assert.That(result.ResolvedExemptions, Is.Empty);
        });
    }

    [Test]
    public async Task MeasureAsync_leavesOutFilesTheIncludeGlobsDoNotCover()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().WithSliceMinimum(70d).Build(),
            new FileCoverageBuilder("Other.Project/Features/Git/Services/Thing.cs").Uncovered(1).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1).Build());

        var result = await service.MeasureAsync(Root);

        Assert.That(result.Slices.Select(slice => slice.Slice), Is.EqualTo(new[] { GitSlice }));
    }

    [Test]
    public async Task MeasureAsync_leavesOutFilesTheExcludeGlobsRemove()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration()
                .WithSliceMinimum(70d)
                .WithExclude("AgentUp.Server/Features/Git/Controllers/**")
                .Build(),
            new FileCoverageBuilder("AgentUp.Server/Features/Git/Controllers/GitController.cs")
                .Uncovered(1, 2, 3).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1).Build());

        Assert.That((await service.MeasureAsync(Root)).Slices.Single().Percent, Is.EqualTo(100d));
    }

    [Test]
    public async Task MeasureAsync_ignoresFilesOutsideAnyFeatureSlice()
    {
        // Shared types and composition roots belong to no slice, so they cannot be scored
        // as one.
        var service = ServiceOver(
            CoverageDomain.Configuration().WithSliceMinimum(70d).Build(),
            new FileCoverageBuilder("AgentUp.Server/Shared/Providers/ClockProvider.cs")
                .Uncovered(1, 2).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1).Build());

        var result = await service.MeasureAsync(Root);

        Assert.That(result.Slices.Select(slice => slice.Slice), Is.EqualTo(new[] { GitSlice }));
    }

    [Test]
    public async Task MeasureAsync_reportsThatNothingWasMeasuredWhenNoReportMentionsASlice()
    {
        var service = ServiceOver(CoverageDomain.Configuration().WithSliceMinimum(70d).Build());

        var result = await service.MeasureAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.MeasuredNothing, Is.True);
            Assert.That(result.Slices, Is.Empty);
        });
    }

    [Test]
    public async Task MeasureAsync_prefersAnExplicitMinimumOverTheConfiguredFloor()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().WithSliceMinimum(10d).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1).Uncovered(2).Build());

        var result = await service.MeasureAsync(Root, minimumOverride: 90d);

        Assert.Multiple(() =>
        {
            Assert.That(result.Minimum, Is.EqualTo(90d));
            Assert.That(result.Failing.Single().Slice, Is.EqualTo(GitSlice));
        });
    }
}

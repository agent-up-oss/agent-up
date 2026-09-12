using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Features.Coverage.Services;
using AgentUp.Verification.Shared.Providers;
using AgentUp.Verification.Tests.Fake;
using AgentUp.Verification.Tests.Support;

namespace AgentUp.Verification.Tests.Features.Coverage.Unit;

[TestFixture]
public sealed class PatchCoverageServiceTests
{
    private const string Root = "/repo";

    /// <summary>
    /// Assembles the service over an explicit world. Each test declares its own inputs
    /// here rather than inheriting a shared fixture.
    /// </summary>
    private static PatchCoverageService ServiceOver(
        CoverageConfiguration configuration,
        ChangedLines changed,
        params FileCoverage[] coverage)
        => new(
            new StubCoverageConfigurationLoader(configuration),
            new StaticCoverageReportReader(CoverageReport.FromFiles(coverage)),
            [new StaticChangedLineSource(changed)],
            new PathGlobProvider());

    [Test]
    public async Task MeasureAsync_scoresFullyCoveredChangedLinesAsSatisfied()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().Build(),
            ChangedLinesBuilder.Changing(CoverageDomain.ServerSource, 10, 11, 12).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(10, 11, 12).Build());

        var result = await service.MeasureAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Percentage, Is.EqualTo(100d));
            Assert.That(result.Satisfied, Is.True);
        });
    }

    [Test]
    public async Task MeasureAsync_failsWhenChangedLinesAreNotCovered()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().Build(),
            ChangedLinesBuilder.Changing(CoverageDomain.ServerSource, 10, 11, 12, 13).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(10, 11).Uncovered(12, 13).Build());

        var result = await service.MeasureAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Percentage, Is.EqualTo(50d));
            Assert.That(result.Satisfied, Is.False);
        });
    }

    [Test]
    public async Task MeasureAsync_namesTheUncoveredLinesSoAnAgentKnowsWhatToTest()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().Build(),
            ChangedLinesBuilder.Changing(CoverageDomain.ServerSource, 10, 11, 12).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(10).Uncovered(11, 12).Build());

        var result = await service.MeasureAsync(Root);

        var uncovered = result.Uncovered.Single();
        Assert.Multiple(() =>
        {
            Assert.That(uncovered.Path, Is.EqualTo(CoverageDomain.ServerSource));
            Assert.That(uncovered.Lines, Is.EqualTo(new[] { 11, 12 }));
        });
    }

    [Test]
    public async Task MeasureAsync_ignoresChangedLinesThatAreNotCoverable()
    {
        // A blank line, a brace, or a record declaration has no sequence point. Counting
        // them would make the ratio depend on formatting.
        var service = ServiceOver(
            CoverageDomain.Configuration().Build(),
            ChangedLinesBuilder.Changing(CoverageDomain.ServerSource, 10, 11, 12).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(10).NotCoverable(11, 12).Build());

        var result = await service.MeasureAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.CoverableLines, Is.EqualTo(1));
            Assert.That(result.Percentage, Is.EqualTo(100d));
        });
    }

    [Test]
    public async Task MeasureAsync_scoresAChangeWithNoCoverableLinesAsSatisfied()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().Build(),
            ChangedLinesBuilder.Changing(CoverageDomain.DocumentationSource, 1, 2).Build());

        var result = await service.MeasureAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Satisfied, Is.True, "A docs edit must not fail a gate it cannot satisfy.");
            Assert.That(result.CoverableLines, Is.Zero);
        });
    }

    [Test]
    public async Task MeasureAsync_appliesTheIncludeGlobSoUnrelatedFilesAreNotMeasured()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().Build(),
            new ChangedLinesBuilder()
                .With(CoverageDomain.ServerSource, 10)
                .With(CoverageDomain.DocumentationSource, 1, 2, 3)
                .Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(10).Build());

        var result = await service.MeasureAsync(Root);

        Assert.That(result.CoverableLines, Is.EqualTo(1), "Only the included source file is measured.");
    }

    [Test]
    public async Task MeasureAsync_appliesTheExcludeGlobAfterInclude()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().Build(),
            ChangedLinesBuilder.Changing(CoverageDomain.CompositionSource, 40, 41).Build(),
            new FileCoverageBuilder(CoverageDomain.CompositionSource).Uncovered(40, 41).Build());

        var result = await service.MeasureAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Satisfied, Is.True);
            Assert.That(result.CoverableLines, Is.Zero, "Composition roots are excluded from the measurement.");
        });
    }

    [Test]
    public async Task MeasureAsync_blocksWhenTheWholeProjectIsMissingFromEveryReport()
    {
        // Means that suite never ran. Treating it as full coverage would turn a skipped
        // test project into a passing gate.
        var service = ServiceOver(
            CoverageDomain.Configuration().Build(),
            ChangedLinesBuilder.Changing(CoverageDomain.ServerSource, 10).Build());

        var result = await service.MeasureAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesWithoutReport, Is.EqualTo(new[] { CoverageDomain.ServerSource }));
            Assert.That(result.Satisfied, Is.False);
        });
    }

    [Test]
    public async Task MeasureAsync_treatsAFileWithNoCodeAsBenignWhenItsProjectIsCovered()
    {
        // A changed interface or enum has no report entry of its own. It must not be
        // mistaken for a suite that failed to run.
        const string interfaceSource = "AgentUp.Server/Features/Git/Interfaces/IGitThing.cs";
        var service = ServiceOver(
            CoverageDomain.Configuration().Build(),
            new ChangedLinesBuilder()
                .With(CoverageDomain.ServerSource, 10)
                .With(interfaceSource, 1, 2, 3)
                .Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(10).Build());

        var result = await service.MeasureAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.FilesWithoutReport, Is.Empty);
            Assert.That(result.Satisfied, Is.True);
            Assert.That(result.CoverableLines, Is.EqualTo(1), "The interface contributes no coverable lines.");
        });
    }

    [Test]
    public async Task MeasureAsync_stillBlocksAnUnreportedFileWhenOnlyAnotherProjectIsCovered()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().Build(),
            new ChangedLinesBuilder()
                .With(CoverageDomain.ServerSource, 10)
                .With(CoverageDomain.CliSource, 20)
                .Build(),
            new FileCoverageBuilder(CoverageDomain.CliSource).Covered(20).Build());

        var result = await service.MeasureAsync(Root);

        Assert.That(result.FilesWithoutReport, Is.EqualTo(new[] { CoverageDomain.ServerSource }),
            "AgentUp.Server has no coverage data at all, so its suite did not run.");
    }

    [Test]
    public async Task MeasureAsync_countsALineCoveredByAnySuiteAsCovered()
    {
        // Two reports for the same file: a line reached by either test project counts.
        var service = ServiceOver(
            CoverageDomain.Configuration().Build(),
            ChangedLinesBuilder.Changing(CoverageDomain.ServerSource, 10, 11).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(10).Uncovered(11).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Uncovered(10).Covered(11).Build());

        var result = await service.MeasureAsync(Root);

        Assert.That(result.Percentage, Is.EqualTo(100d));
    }

    [Test]
    public async Task MeasureAsync_honoursAMinimumOverride()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().WithMinimum(90d).Build(),
            ChangedLinesBuilder.Changing(CoverageDomain.ServerSource, 10, 11).Build(),
            new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(10).Uncovered(11).Build());

        var strict = await service.MeasureAsync(Root);
        var lenient = await service.MeasureAsync(Root, minimumOverride: 50d);

        Assert.Multiple(() =>
        {
            Assert.That(strict.Satisfied, Is.False);
            Assert.That(lenient.Satisfied, Is.True);
            Assert.That(lenient.Minimum, Is.EqualTo(50d));
        });
    }

    [Test]
    public async Task MeasureAsync_measuresNothingWhenTheRepositoryHasNoCoverageSection()
    {
        var service = ServiceOver(
            CoverageDomain.Configuration().WithoutInclude().Build(),
            ChangedLinesBuilder.Changing(CoverageDomain.ServerSource, 10).Build());

        var result = await service.MeasureAsync(Root);

        Assert.That(result.Satisfied, Is.True);
    }

    [Test]
    public async Task MeasureAsync_mergesEveryRegisteredChangedLineSource()
    {
        var service = new PatchCoverageService(
            new StubCoverageConfigurationLoader(CoverageDomain.Configuration().Build()),
            new StaticCoverageReportReader(CoverageReport.FromFiles([
                new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(10).Build(),
                new FileCoverageBuilder(CoverageDomain.CliSource).Uncovered(20).Build()
            ])),
            [
                new StaticChangedLineSource(ChangedLinesBuilder.Changing(CoverageDomain.ServerSource, 10).Build()),
                new StaticChangedLineSource(ChangedLinesBuilder.Changing(CoverageDomain.CliSource, 20).Build())
            ],
            new PathGlobProvider());

        var result = await service.MeasureAsync(Root);

        Assert.Multiple(() =>
        {
            Assert.That(result.CoverableLines, Is.EqualTo(2));
            Assert.That(result.Percentage, Is.EqualTo(50d));
        });
    }
}

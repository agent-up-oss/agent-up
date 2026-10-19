using AgentUp.Verification.Features.Coverage.Controllers;
using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Features.Coverage.Services;
using AgentUp.Verification.Shared.Providers;
using AgentUp.Verification.Tests.Fake;
using AgentUp.Verification.Tests.Support;

namespace AgentUp.Verification.Tests.Features.Coverage.Controller;

[TestFixture]
public sealed class CoverageControllerTests
{
    private const string Root = "/repo";

    [Test]
    public async Task PatchMeasureAsync_returnsTheServiceResult()
    {
        var controller = new PatchCoverageController(new PatchCoverageService(
            new StubCoverageConfigurationLoader(CoverageDomain.Configuration().Build()),
            new StaticCoverageReportReader(CoverageReport.FromFiles(
                [new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(10, 11, 12).Build()])),
            [new StaticChangedLineSource(ChangedLinesBuilder.Changing(CoverageDomain.ServerSource, 10, 11, 12).Build())],
            new PathGlobProvider()));

        var result = await controller.MeasureAsync(Root, null, CancellationToken.None);

        Assert.That(result.Percentage, Is.EqualTo(100d));
    }

    [Test]
    public async Task SliceMeasureAsync_returnsTheServiceResult()
    {
        var controller = new SliceCoverageController(new SliceCoverageService(
            new StubCoverageConfigurationLoader(CoverageDomain.Configuration().WithSliceMinimum(70d).Build()),
            new StaticCoverageReportReader(CoverageReport.FromFiles(
                [new FileCoverageBuilder(CoverageDomain.ServerSource).Covered(1, 2, 3).Uncovered(4).Build()])),
            new PathGlobProvider()));

        var result = await controller.MeasureAsync(Root, null, CancellationToken.None);

        Assert.That(result.Slices.Single().Percent, Is.EqualTo(75d));
    }
}

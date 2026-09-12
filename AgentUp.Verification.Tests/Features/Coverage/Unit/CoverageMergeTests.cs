using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Tests.Support;

namespace AgentUp.Verification.Tests.Features.Coverage.Unit;

[TestFixture]
public sealed class CoverageMergeTests
{
    private const string Path = "AgentUp.Server/Features/Git/Services/GitChangesService.cs";

    [Test]
    public void MergeWith_treatsALineCoveredByEitherReportAsCovered()
    {
        var merged = new FileCoverageBuilder(Path).Covered(10).Uncovered(11).Build()
            .MergeWith(new FileCoverageBuilder(Path).Uncovered(10).Covered(11).Build());

        Assert.Multiple(() =>
        {
            Assert.That(merged.IsCovered(10), Is.True);
            Assert.That(merged.IsCovered(11), Is.True);
        });
    }

    [Test]
    public void MergeWith_keepsALineUncoveredWhenNoReportHitsIt()
    {
        var merged = new FileCoverageBuilder(Path).Uncovered(12).Build()
            .MergeWith(new FileCoverageBuilder(Path).Uncovered(12).Build());

        Assert.Multiple(() =>
        {
            Assert.That(merged.IsCoverable(12), Is.True);
            Assert.That(merged.IsCovered(12), Is.False);
        });
    }

    [Test]
    public void MergeWith_unionsCoverableLinesFromBothReports()
    {
        var merged = new FileCoverageBuilder(Path).Covered(10).Build()
            .MergeWith(new FileCoverageBuilder(Path).Covered(20).Build());

        Assert.That(merged.LineHits.Keys, Is.EquivalentTo(new[] { 10, 20 }));
    }

    [Test]
    public void FromFiles_mergesReportsForTheSamePathAndKeepsOthersSeparate()
    {
        var report = CoverageReport.FromFiles([
            new FileCoverageBuilder(Path).Covered(10).Build(),
            new FileCoverageBuilder(Path).Covered(11).Build(),
            new FileCoverageBuilder("AgentUp.CLI/Features/X/Services/Y.cs").Covered(1).Build()
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(report.Files, Has.Count.EqualTo(2));
            Assert.That(report.Find(Path)!.LineHits.Keys, Is.EquivalentTo(new[] { 10, 11 }));
        });
    }

    [Test]
    public void Find_returnsNullForAPathNoReportMentions()
    {
        Assert.That(CoverageReport.Empty.Find(Path), Is.Null);
    }

    [Test]
    public void ChangedLines_mergeWithUnionsLineSetsPerPath()
    {
        var merged = ChangedLinesBuilder.Changing(Path, 1, 2).Build()
            .MergeWith(ChangedLinesBuilder.Changing(Path, 2, 3).Build());

        Assert.Multiple(() =>
        {
            Assert.That(merged.Lines[Path], Is.EquivalentTo(new[] { 1, 2, 3 }));
            Assert.That(merged.TotalLines, Is.EqualTo(3));
        });
    }
}

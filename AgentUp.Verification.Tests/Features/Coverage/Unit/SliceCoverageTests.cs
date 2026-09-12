using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.Verification.Tests.Features.Coverage.Unit;

[TestFixture]
public sealed class SliceCoverageTests
{
    private const string Slice = "AgentUp.Server/Features/Git";

    [Test]
    public void Percent_isTheCoveredShareOfCoverableLines()
    {
        Assert.That(new SliceCoverage(Slice, 3, 4).Percent, Is.EqualTo(75d));
    }

    [Test]
    public void Percent_isFullForASliceWithNoCoverableLines()
    {
        // A slice of interfaces and enums has nothing to cover, so it cannot be the thing
        // holding the floor down.
        Assert.That(new SliceCoverage(Slice, 0, 0).Percent, Is.EqualTo(100d));
    }

    [Test]
    public void Meets_acceptsASliceExactlyAtTheFloor()
    {
        Assert.That(new SliceCoverage(Slice, 7, 10).Meets(70d), Is.True);
    }

    [Test]
    public void Meets_rejectsASliceJustUnderTheFloor()
    {
        Assert.That(new SliceCoverage(Slice, 69, 100).Meets(70d), Is.False);
    }

    [Test]
    public void Add_accumulatesAnotherFilesLines()
    {
        var total = new SliceCoverage(Slice, 1, 2).Add(3, 4);

        Assert.Multiple(() =>
        {
            Assert.That(total.Covered, Is.EqualTo(4));
            Assert.That(total.Coverable, Is.EqualTo(6));
            Assert.That(total.Slice, Is.EqualTo(Slice));
        });
    }
}

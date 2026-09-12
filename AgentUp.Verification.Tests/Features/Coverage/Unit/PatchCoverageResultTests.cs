using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.Verification.Tests.Features.Coverage.Unit;

[TestFixture]
public sealed class PatchCoverageResultTests
{
    [Test]
    public void Percentage_isOneHundredWhenNothingIsCoverable()
    {
        Assert.That(PatchCoverageResult.NothingToCover(90d).Percentage, Is.EqualTo(100d));
    }

    [TestCase(9, 10, 90d, true)]
    [TestCase(89, 100, 90d, false)]
    [TestCase(90, 100, 90d, true)]
    [TestCase(0, 1, 90d, false)]
    public void Satisfied_comparesAgainstTheMinimumInclusively(
        int covered, int coverable, double minimum, bool expected)
    {
        var result = new PatchCoverageResult(covered, coverable, minimum, [], []);

        Assert.That(result.Satisfied, Is.EqualTo(expected));
    }

    [Test]
    public void Satisfied_isFalseWhenAFileHasNoReportEvenAtFullPercentage()
    {
        var result = new PatchCoverageResult(10, 10, 90d, [], ["AgentUp.Server/Features/X/Services/Y.cs"]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Percentage, Is.EqualTo(100d));
            Assert.That(result.Satisfied, Is.False);
        });
    }
}

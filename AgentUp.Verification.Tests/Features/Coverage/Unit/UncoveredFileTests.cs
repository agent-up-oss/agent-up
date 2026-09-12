using AgentUp.Verification.Features.Coverage.Models;

namespace AgentUp.Verification.Tests.Features.Coverage.Unit;

[TestFixture]
public sealed class UncoveredFileTests
{
    private static string Ranges(params int[] lines)
        => new UncoveredFile("f.cs", lines).DescribeRanges();

    [Test]
    public void DescribeRanges_collapsesAConsecutiveRun()
    {
        Assert.That(Ranges(10, 11, 12), Is.EqualTo("10-12"));
    }

    [Test]
    public void DescribeRanges_keepsIsolatedLinesSeparate()
    {
        Assert.That(Ranges(10, 14, 20), Is.EqualTo("10, 14, 20"));
    }

    [Test]
    public void DescribeRanges_mixesRunsAndIsolatedLines()
    {
        Assert.That(Ranges(10, 11, 12, 20, 30, 31), Is.EqualTo("10-12, 20, 30-31"));
    }

    [Test]
    public void DescribeRanges_sortsUnorderedInput()
    {
        Assert.That(Ranges(12, 10, 11), Is.EqualTo("10-12"));
    }

    [Test]
    public void DescribeRanges_handlesASingleLine()
    {
        Assert.That(Ranges(7), Is.EqualTo("7"));
    }

    [Test]
    public void DescribeRanges_handlesNoLines()
    {
        Assert.That(Ranges(), Is.Empty);
    }
}

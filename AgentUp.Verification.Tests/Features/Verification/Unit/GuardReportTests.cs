using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Tests.Support;

namespace AgentUp.Verification.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class GuardReportTests
{
    private static CheckVerdict Verdict(CheckVerdictKind kind)
        => new(VerificationDomain.ServerUnitCheck, "dotnet test", kind, "detail");

    [TestCase(CheckVerdictKind.NeverRun, true)]
    [TestCase(CheckVerdictKind.Failed, true)]
    [TestCase(CheckVerdictKind.Stale, true)]
    [TestCase(CheckVerdictKind.Satisfied, false)]
    [TestCase(CheckVerdictKind.Skipped, false)]
    public void Blocking_countsUnprovenChecksButNotSkippedOnes(CheckVerdictKind kind, bool expected)
    {
        Assert.That(Verdict(kind).Blocking, Is.EqualTo(expected));
    }

    [Test]
    public void Satisfied_isFalseWhenFilesMatchedNoRule()
    {
        var report = new GuardReport(
            [Verdict(CheckVerdictKind.Satisfied)],
            VerificationEnforcement.Block,
            [VerificationDomain.UnmappedSource],
            1);

        Assert.That(report.Satisfied, Is.False,
            "An incomplete path map must not read as a pass.");
    }

    [Test]
    public void ShouldBlock_isFalseUnderWarnEnforcementEvenWhenUnsatisfied()
    {
        var report = new GuardReport(
            [Verdict(CheckVerdictKind.NeverRun)],
            VerificationEnforcement.Warn,
            [],
            1);

        Assert.Multiple(() =>
        {
            Assert.That(report.Satisfied, Is.False);
            Assert.That(report.ShouldBlock, Is.False);
        });
    }

    [Test]
    public void ShouldBlock_isTrueUnderBlockEnforcementWhenUnsatisfied()
    {
        var report = new GuardReport(
            [Verdict(CheckVerdictKind.Stale)],
            VerificationEnforcement.Block,
            [],
            1);

        Assert.That(report.ShouldBlock, Is.True);
    }

    [Test]
    public void ShouldBlock_isFalseWhenOnlySkippedChecksRemain()
    {
        var report = new GuardReport(
            [Verdict(CheckVerdictKind.Skipped), Verdict(CheckVerdictKind.Satisfied)],
            VerificationEnforcement.Block,
            [],
            1);

        Assert.Multiple(() =>
        {
            Assert.That(report.Satisfied, Is.True);
            Assert.That(report.ShouldBlock, Is.False);
            Assert.That(report.SkippedVerdicts, Has.Count.EqualTo(1));
        });
    }
}

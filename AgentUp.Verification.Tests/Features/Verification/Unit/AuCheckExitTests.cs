using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Tests.Support;

namespace AgentUp.Verification.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class AuCheckExitTests
{
    [Test]
    public void ForRun_usesDistinctCodesForFailureToolchainAndAnIncompleteMap()
    {
        var failed = Outcome(false);
        var passed = Outcome(true);
        var toolchain = Plan(CheckSkipReason.ToolchainMissing, unmatched: false);
        var unmatched = Plan(CheckSkipReason.None, unmatched: true);

        Assert.Multiple(() =>
        {
            Assert.That(AuCheckExit.ForRun(toolchain, [passed]).Code, Is.EqualTo(AuCheckExit.CannotRun));
            Assert.That(AuCheckExit.ForRun(Plan(CheckSkipReason.None, unmatched: false), [failed]).Code, Is.EqualTo(AuCheckExit.Failed));
            Assert.That(AuCheckExit.ForRun(unmatched, [passed]).Code, Is.EqualTo(AuCheckExit.Invalid));
            Assert.That(AuCheckExit.ForRun(Plan(CheckSkipReason.CiOnly, unmatched: false), []).Code, Is.EqualTo(AuCheckExit.Passed));
        });
    }

    [Test]
    public void ForGuard_doesNotTreatAPlatformSkipAsUnableToRun()
    {
        var skipped = new CheckVerdict("macos", "./smoke.sh", CheckVerdictKind.Skipped, "platform", CheckSkipReason.PlatformMismatch);
        var report = new GuardReport([skipped], VerificationEnforcement.Block, [], 1);

        Assert.That(AuCheckExit.ForGuard(report).Code, Is.EqualTo(AuCheckExit.Passed));
    }

    [Test]
    public void ForGuard_returnsCannotRunWhenTheOnlyBlockIsAMissingToolchain()
    {
        var skipped = new CheckVerdict("image", "docker info", CheckVerdictKind.Skipped, "docker is not installed", CheckSkipReason.ToolchainMissing);
        var report = new GuardReport([skipped], VerificationEnforcement.Block, [], 1);

        Assert.Multiple(() =>
        {
            Assert.That(AuCheckExit.ForGuard(report).Code, Is.EqualTo(AuCheckExit.CannotRun));
            Assert.That(AuCheckExit.ForGuard(report).Reason, Is.EqualTo(AuCheckExit.ReasonCannotRun));
        });
    }

    private static CheckOutcome Outcome(bool succeeded)
        => new("unit", "dotnet test", succeeded ? 0 : 1, 1, "output");

    private static VerificationPlan Plan(CheckSkipReason reason, bool unmatched)
    {
        var definition = new CheckBuilder("unit").Build();
        var check = new PlannedCheck(
            definition,
            new Dictionary<string, string>(StringComparer.Ordinal),
            reason,
            ["src/**"]);
        return new VerificationPlan(
            [check],
            new Dictionary<string, string>(StringComparer.Ordinal) { ["src/A.cs"] = "sha256:1" },
            unmatched ? ["orphan.txt"] : []);
    }
}

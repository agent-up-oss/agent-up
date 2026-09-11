using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Tests.Support;

namespace AgentUp.Verification.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class ReceiptLedgerTests
{
    [Test]
    public void With_replacesTheExistingReceiptForTheSameCheck()
    {
        var ledger = ReceiptLedger.Empty
            .With(new ReceiptBuilder(VerificationDomain.ServerUnitCheck).WithExitCode(1).Build())
            .With(new ReceiptBuilder(VerificationDomain.ServerUnitCheck).WithExitCode(0).Build());

        Assert.Multiple(() =>
        {
            Assert.That(ledger.Receipts, Has.Count.EqualTo(1),
                "A stale failure must not sit alongside a fresh success for the same check.");
            Assert.That(ledger.Find(VerificationDomain.ServerUnitCheck)!.Succeeded, Is.True);
        });
    }

    [Test]
    public void With_keepsReceiptsForOtherChecks()
    {
        var ledger = ReceiptLedger.Empty
            .With(new ReceiptBuilder(VerificationDomain.ArchitectureCheck).Build())
            .With(new ReceiptBuilder(VerificationDomain.ServerUnitCheck).Build());

        Assert.That(ledger.Receipts.Select(receipt => receipt.CheckId),
            Is.EquivalentTo(new[] { VerificationDomain.ArchitectureCheck, VerificationDomain.ServerUnitCheck }));
    }

    [Test]
    public void Find_returnsNullForACheckWithNoReceipt()
    {
        Assert.That(ReceiptLedger.Empty.Find(VerificationDomain.MobileCheck), Is.Null);
    }

    [Test]
    public void Succeeded_isTrueOnlyForExitCodeZero()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new ReceiptBuilder(VerificationDomain.MobileCheck).WithExitCode(0).Build().Succeeded, Is.True);
            Assert.That(new ReceiptBuilder(VerificationDomain.MobileCheck).WithExitCode(1).Build().Succeeded, Is.False);
        });
    }
}

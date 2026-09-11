using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;
using AgentUp.Verification.Tests.Support;

namespace AgentUp.Verification.Tests.Features.Verification.Provider;

[TestFixture]
public sealed class FileReceiptLedgerStoreTests
{
    private static string CreateCheckout()
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, "ledger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Join(root, ".git"));
        return root;
    }

    private static FileReceiptLedgerStore Store() => new(new GitDirectoryProvider());

    [Test]
    public async Task ReadAsync_returnsEmptyForACheckoutWithNoLedgerYet()
    {
        var ledger = await Store().ReadAsync(CreateCheckout(), CancellationToken.None);

        Assert.That(ledger.Receipts, Is.Empty);
    }

    [Test]
    public async Task WriteAsync_thenReadAsync_roundTripsTheCoveredMap()
    {
        var root = CreateCheckout();
        var receipt = new ReceiptBuilder(VerificationDomain.ServerUnitCheck)
            .WithCommand("dotnet test AgentUp.Server.Tests")
            .WithCovered(ChangeSetBuilder.Changing(VerificationDomain.ServerSource).Build())
            .Build();

        await Store().WriteAsync(root, ReceiptLedger.Empty.With(receipt), CancellationToken.None);
        var reloaded = await Store().ReadAsync(root, CancellationToken.None);

        var found = reloaded.Find(VerificationDomain.ServerUnitCheck);
        Assert.Multiple(() =>
        {
            Assert.That(found, Is.Not.Null);
            Assert.That(found!.Command, Is.EqualTo("dotnet test AgentUp.Server.Tests"));
            Assert.That(found.Covered[VerificationDomain.ServerSource],
                Is.EqualTo(ChangeSetBuilder.StableHashFor(VerificationDomain.ServerSource)));
        });
    }

    [Test]
    public async Task WriteAsync_storesReceiptsInsideTheGitDirectorySoTheyAreNeverCommitted()
    {
        var root = CreateCheckout();

        await Store().WriteAsync(
            root,
            ReceiptLedger.Empty.With(new ReceiptBuilder(VerificationDomain.ArchitectureCheck).Build()),
            CancellationToken.None);

        Assert.That(File.Exists(Path.Join(root, ".git", "agent-up", "verification", "receipts.json")), Is.True);
    }

    [Test]
    public async Task ReadAsync_failsSafeToEmptyWhenTheLedgerIsCorrupt()
    {
        var root = CreateCheckout();
        var ledgerDirectory = Path.Join(root, ".git", "agent-up", "verification");
        Directory.CreateDirectory(ledgerDirectory);
        File.WriteAllText(Path.Join(ledgerDirectory, "receipts.json"), "{ not json");

        var ledger = await Store().ReadAsync(root, CancellationToken.None);

        Assert.That(ledger.Receipts, Is.Empty,
            "An unreadable ledger must mean 'nothing is proven', not 'trust it'.");
    }

    [Test]
    public void WriteAsync_throwsWhenTheDirectoryIsNotAGitCheckout()
    {
        var root = Path.Join(TestContext.CurrentContext.WorkDirectory, "plain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        Assert.That(async () => await Store().WriteAsync(root, ReceiptLedger.Empty, CancellationToken.None),
            Throws.TypeOf<InvalidOperationException>());
    }
}

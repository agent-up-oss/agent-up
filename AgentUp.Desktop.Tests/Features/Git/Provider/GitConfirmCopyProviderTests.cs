using AgentUp.Desktop.Features.Git.Providers;

namespace AgentUp.Desktop.Tests.Features.Git.Provider;

[TestFixture]
public sealed class GitConfirmCopyProviderTests
{
    [Test]
    public void Push_namesTheCurrentBranchAndUpstream()
    {
        var copy = GitConfirmCopyProvider.Push("main", "origin/main");

        Assert.That(copy.Title, Is.EqualTo("Push to upstream?"));
        Assert.That(copy.Message, Does.Contain("main").And.Contain("origin/main"));
        Assert.That(copy.Confirm, Is.EqualTo("Push"));
        Assert.That(copy.Destructive, Is.False);
    }

    [Test]
    public void PushFailureOffersForce_mapsNonFastForwardAndLeaseFailures()
    {
        Assert.That(GitConfirmCopyProvider.PushFailureOffersForce("The remote rejected a non-fast-forward update."), Is.True);
        Assert.That(GitConfirmCopyProvider.PushFailureOffersForce("failed to push some refs to origin"), Is.True);
        Assert.That(GitConfirmCopyProvider.PushFailureOffersForce("Updates were rejected because the tip of your current branch is behind"), Is.True);
        Assert.That(GitConfirmCopyProvider.PushFailureOffersForce("Force-with-lease failed: stale info"), Is.True);
        Assert.That(GitConfirmCopyProvider.PushFailureOffersForce("Git push could not authenticate to the remote."), Is.False);
        Assert.That(GitConfirmCopyProvider.PushFailureOffersForce("This branch has no upstream. Push with setUpstream to create one."), Is.False);
        Assert.That(GitConfirmCopyProvider.PushFailureOffersForce(null), Is.False);
    }

    [Test]
    public void PushRejected_offersForceWithLease()
    {
        var copy = GitConfirmCopyProvider.PushRejected(
            "The remote rejected a non-fast-forward update.",
            "main",
            "origin/main");

        Assert.That(copy.Title, Is.EqualTo("Push rejected"));
        Assert.That(copy.Message, Does.Contain("non-fast-forward").And.Contain("--force-with-lease"));
        Assert.That(copy.Confirm, Does.Contain("Force push"));
        Assert.That(copy.Destructive, Is.True);
    }

    [Test]
    public void Discard_listsTheSelectedPaths()
    {
        var copy = GitConfirmCopyProvider.Discard(["src/app/main.cs", "README.md"]);

        Assert.That(copy.Title, Is.EqualTo("Discard selected files?"));
        Assert.That(copy.Message, Does.Contain("src/app/main.cs").And.Contain("README.md"));
        Assert.That(copy.Confirm, Is.EqualTo("Discard"));
        Assert.That(copy.Destructive, Is.True);
    }
}

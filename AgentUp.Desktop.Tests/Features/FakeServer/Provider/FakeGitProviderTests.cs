using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.Providers;

namespace AgentUp.Desktop.Tests.Features.FakeServer.Provider;

[TestFixture]
public sealed class FakeGitProviderTests
{
    [Test]
    public void Commit_removesSelectedFilesAndAdvancesAhead()
    {
        var git = HarborGit();
        var result = FakeGitProvider.Commit(git, ["apps/storefront/ProductGrid.tsx"], "fix(storefront): featured grid");

        Assert.That(result["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(git["changes"]!["fileCount"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(FakeGitProvider.Head(git)["ahead"]!.GetValue<int>(), Is.EqualTo(1));
        Assert.That(FakeGitProvider.Head(git)["behind"]!.GetValue<int>(), Is.EqualTo(1));
    }

    [Test]
    public void FetchPullAndPush_updateAheadBehind()
    {
        var git = HarborGit();
        FakeGitProvider.Fetch(git);
        FakeGitProvider.Fetch(git);
        Assert.That(FakeGitProvider.Head(git)["behind"]!.GetValue<int>(), Is.EqualTo(1));
        FakeGitProvider.Pull(git);
        FakeGitProvider.Pull(git);
        Assert.That(FakeGitProvider.Head(git)["behind"]!.GetValue<int>(), Is.EqualTo(0));
        Assert.That(OriginCommits(git), Is.EqualTo(1));
        FakeGitProvider.Fetch(git);
        FakeGitProvider.Pull(git);
        Assert.That(FakeGitProvider.Head(git)["behind"]!.GetValue<int>(), Is.EqualTo(0));
        Assert.That(OriginCommits(git), Is.EqualTo(1));
        Assert.That(FakeGitProvider.Commit(git, ["apps/storefront/ProductGrid.tsx"], "fix(storefront): featured grid")["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(FakeGitProvider.Head(git)["behind"]!.GetValue<int>(), Is.EqualTo(0));
        git["changes"]!["ahead"] = 2;
        FakeGitProvider.Push(git);
        Assert.That(FakeGitProvider.Head(git)["ahead"]!.GetValue<int>(), Is.EqualTo(0));
    }

    [Test]
    public void AddAgentFile_appendsAnUncommittedStorefrontFile()
    {
        var git = HarborGit();
        var path = FakeGitProvider.AddAgentFile(git);
        var discarded = FakeGitProvider.Discard(git, [path]);

        Assert.That(path, Does.StartWith("apps/storefront/PromoBanner"));
        Assert.That(FakeGitProvider.Diff(git, path), Is.Null);
        Assert.That(discarded["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(git["changes"]!["fileCount"]!.GetValue<int>(), Is.EqualTo(2));
    }

    [Test]
    public void Commit_rejectsUnknownFilesAndUsesADefaultMessage()
    {
        var git = SparseGit();
        var rejected = FakeGitProvider.Commit(git, ["missing.ts"], "fix");
        var committed = FakeGitProvider.Commit(git, ["README.md"], "   ");

        Assert.That(rejected["succeeded"]!.GetValue<bool>(), Is.False);
        Assert.That(rejected["error"]!.GetValue<string>(), Does.Contain("Select at least one file"));
        Assert.That(committed["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(git["log"]!["commits"]![0]!["subject"]!.GetValue<string>(), Is.EqualTo("chore: update harbor shop"));
        Assert.That(FakeGitProvider.Diff(git, "README.md"), Is.Null);
    }

    [Test]
    public void Head_defaultsMissingChangeFields()
    {
        var git = new JsonObject { ["changes"] = new JsonObject() };
        var head = FakeGitProvider.Head(git);

        Assert.That(head["branch"]!.GetValue<string>(), Is.EqualTo("main"));
        Assert.That(head["upstream"]!.GetValue<string>(), Is.EqualTo("origin/main"));
        Assert.That(head["ahead"]!.GetValue<int>(), Is.EqualTo(0));
        Assert.That(head["behind"]!.GetValue<int>(), Is.EqualTo(0));
        Assert.That(head["commit"], Is.Null);
        Assert.That(FakeGitProvider.Discard(git, ["README.md"])["succeeded"]!.GetValue<bool>(), Is.True);
    }

    [Test]
    public void Head_throwsWhenChangesAreMissing()
    {
        Assert.That(
            () => FakeGitProvider.Head([]),
            Throws.InvalidOperationException.With.Message.EqualTo("The fake git node is missing changes."));
    }

    [Test]
    public void SwitchBranch_createsAndRejectsMissingNames()
    {
        var git = HarborGit();
        var empty = FakeGitProvider.SwitchBranch(git, "  ", create: true);
        var missing = FakeGitProvider.SwitchBranch(git, "topic", create: false);
        var created = FakeGitProvider.SwitchBranch(git, "topic", create: true);
        var existing = FakeGitProvider.SwitchBranch(git, "main", create: true);

        Assert.That(empty["succeeded"]!.GetValue<bool>(), Is.False);
        Assert.That(missing["succeeded"]!.GetValue<bool>(), Is.False);
        Assert.That(created["head"]!["branch"]!.GetValue<string>(), Is.EqualTo("topic"));
        Assert.That(existing["head"]!["branch"]!.GetValue<string>(), Is.EqualTo("main"));
    }

    [Test]
    public void CheckoutRemote_addsALocalBranchFromARemoteName()
    {
        var git = HarborGit();
        var empty = FakeGitProvider.CheckoutRemote(git, " ");
        var remote = FakeGitProvider.CheckoutRemote(git, "origin/release");
        var existing = FakeGitProvider.CheckoutRemote(git, "main");

        Assert.That(empty["succeeded"]!.GetValue<bool>(), Is.False);
        Assert.That(remote["head"]!["branch"]!.GetValue<string>(), Is.EqualTo("release"));
        Assert.That(existing["head"]!["branch"]!.GetValue<string>(), Is.EqualTo("main"));
        Assert.That(
            git["changes"]!["localBranches"]!.AsArray().Select(item => item!.GetValue<string>()),
            Does.Contain("release"));
    }

    [Test]
    public void AddAgentFile_numbersLaterFilesAndRebuildsMissingBookkeeping()
    {
        var git = SparseGit();
        git.Remove("queue");
        git.Remove("diffs");
        git.Remove("log");
        git["nextCommit"] = 4L;
        git["nextAgentFile"] = "nope";
        var first = FakeGitProvider.AddAgentFile(git);
        var second = FakeGitProvider.AddAgentFile(git);

        Assert.That(first, Is.EqualTo("apps/storefront/PromoBanner.tsx"));
        Assert.That(second, Is.EqualTo("apps/storefront/PromoBanner2.tsx"));
        Assert.That(FakeGitProvider.Diff(git, second)!["status"]!.GetValue<string>(), Is.EqualTo("Added"));
        Assert.That(git["queue"]!["unassignedFiles"]!.AsArray().Count, Is.EqualTo(2));
    }

    [Test]
    public void Fetch_reusesAnExistingIncomingCommit()
    {
        var git = HarborGit();
        git["changes"]!["behind"] = 2;
        git["incomingCommit"] = "f4keincoming";
        FakeGitProvider.Fetch(git);
        FakeGitProvider.Pull(git);

        Assert.That(FakeGitProvider.Head(git)["behind"]!.GetValue<int>(), Is.EqualTo(0));
        Assert.That(git["log"]!["commits"]![0]!["id"]!.GetValue<string>(), Is.EqualTo("f4keincoming"));
        Assert.That(git["incomingPulled"]!.GetValue<bool>(), Is.True);
    }

    [Test]
    public void Collect_skipsBlankPathsAndNormalizesModifiedStatus()
    {
        var git = SparseGit();
        var committed = FakeGitProvider.Commit(git, ["README.md"], "docs: readme");

        Assert.That(committed["succeeded"]!.GetValue<bool>(), Is.True);
        Assert.That(git["changes"]!["fileCount"]!.GetValue<int>(), Is.EqualTo(0));
        Assert.That(FakeGitProvider.Diff(git, "README.md"), Is.Null);
    }

    private static int OriginCommits(JsonObject git)
        => (git["log"]?["commits"] as JsonArray ?? [])
            .Count(item => item?["author"]?.GetValue<string>() == "origin");

    private static JsonObject HarborGit()
        => new FakeServerDefinitionProvider().LoadEmbedded().Git!["harbor-shop"]!.DeepClone().AsObject();

    private static JsonObject SparseGit()
        => new JsonObject
        {
            ["changes"] = new JsonObject
            {
                ["root"] = new JsonObject
                {
                    ["name"] = "",
                    ["path"] = "",
                    ["files"] = new JsonArray(
                        new JsonObject { ["path"] = "README.md", ["status"] = "modified" },
                        new JsonObject { ["path"] = "   " },
                        new JsonObject { ["name"] = "skip" })
                }
            }
        };
}

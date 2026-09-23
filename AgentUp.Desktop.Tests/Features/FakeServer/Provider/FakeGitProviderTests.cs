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

    private static int OriginCommits(JsonObject git)
        => (git["log"]?["commits"] as JsonArray ?? [])
            .Count(item => item?["author"]?.GetValue<string>() == "origin");

    private static JsonObject HarborGit()
        => new FakeServerDefinitionProvider().LoadEmbedded().Git!["harbor-shop"]!.DeepClone().AsObject();
}

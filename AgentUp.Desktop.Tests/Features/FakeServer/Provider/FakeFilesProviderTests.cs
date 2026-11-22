using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.Models;
using AgentUp.Desktop.Features.FakeServer.Providers;

namespace AgentUp.Desktop.Tests.Features.FakeServer.Provider;

[TestFixture]
public sealed class FakeFilesProviderTests
{
    private static readonly FakeGitFile Modified = new("apps/storefront/ProductGrid.tsx", "ProductGrid.tsx", "Modified", "");

    [Test]
    public void Directory_listsFoldersFirstAndCountsChangesUnderThem()
    {
        var listing = FakeFilesProvider.Directory("bob", Repository(), Edits(), [Modified], null);

        var entries = listing.Body["entries"]!.AsArray();
        Assert.That(entries.Select(entry => entry!["name"]!.GetValue<string>()), Is.EqualTo(new[] { "apps", "docs", "README.md" }));
        Assert.That(entries[0]!["changedFiles"]!.GetValue<int>(), Is.EqualTo(1));
    }

    [Test]
    public void Directory_listsOneFolderWithEachFileStatus()
    {
        var listing = FakeFilesProvider.Directory("bob", Repository(), Edits(), [Modified], "apps/storefront");

        var entry = listing.Body["entries"]!.AsArray().Single()!;
        Assert.That(entry["path"]!.GetValue<string>(), Is.EqualTo("apps/storefront/ProductGrid.tsx"));
        Assert.That(entry["status"]!.GetValue<string>(), Is.EqualTo("Modified"));
    }

    [Test]
    public void Directory_leavesOutDeletedFilesAndReadsAddedOnesFromTheirDiff()
    {
        FakeGitFile[] changes =
        [
            new("docs/old.md", "old.md", "Deleted", ""),
            new("apps/storefront/PromoBanner.tsx", "PromoBanner.tsx", "Added", "--- /dev/null\n+++ b/apps/storefront/PromoBanner.tsx\n@@ -0,0 +1,1 @@\n+export function PromoBanner() {}\n"),
        ];

        var top = FakeFilesProvider.Directory("bob", Repository(), Edits(), changes, "");
        var added = FakeFilesProvider.File(Repository(), Edits(), changes, "apps/storefront/PromoBanner.tsx");

        Assert.That(top.Body["entries"]!.AsArray().Any(entry => entry!["name"]!.GetValue<string>() == "docs"), Is.False);
        Assert.That(added.Body["content"]!.GetValue<string>(), Is.EqualTo("export function PromoBanner() {}\n"));
    }

    [TestCase("../secret")]
    [TestCase(".git/config")]
    public void File_refusesPathsOutsideTheWorkspace(string path)
        => Assert.That(FakeFilesProvider.File(Repository(), Edits(), [], path).Status, Is.EqualTo(400));

    [Test]
    public void File_andDirectory_areNotFoundWhenMissing()
    {
        Assert.That(FakeFilesProvider.File(Repository(), Edits(), [], "nowhere.md").Status, Is.EqualTo(404));
        Assert.That(FakeFilesProvider.Directory("bob", Repository(), Edits(), [], "nowhere").Status, Is.EqualTo(404));
    }

    [Test]
    public void Write_savesFromTheCurrentVersionAndReportsTheChange()
    {
        var edits = Edits();
        var opened = FakeFilesProvider.File(Repository(), edits, [], "README.md");

        var saved = FakeFilesProvider.Write(Repository(), edits, [], "README.md", "# Harbor Shop\n\nEdited.\n", opened.Body["version"]!.GetValue<string>());

        Assert.That(saved.Status, Is.EqualTo(200));
        Assert.That(edits["README.md"], Is.EqualTo("# Harbor Shop\n\nEdited.\n"));
        Assert.That(saved.Change!.Diff, Does.Contain("+Edited."));
    }

    [Test]
    public void Write_refusesAStaleVersion()
    {
        var edits = Edits();

        var saved = FakeFilesProvider.Write(Repository(), edits, [], "README.md", "mine", FakeFilesProvider.Version("older"));

        Assert.That(saved.Status, Is.EqualTo(409));
        Assert.That(edits, Is.Empty);
    }

    [Test]
    public void Write_requiresContent()
        => Assert.That(FakeFilesProvider.Write(Repository(), Edits(), [], "README.md", null, "v").Status, Is.EqualTo(400));

    [Test]
    public void Directory_refusesAPathOutsideTheWorkspace()
        => Assert.That(FakeFilesProvider.Directory("bob", Repository(), Edits(), [], "../etc").Status, Is.EqualTo(400));

    [Test]
    public void Write_isNotFoundForAMissingFile()
        => Assert.That(FakeFilesProvider.Write(Repository(), Edits(), [], "missing.md", "x", "v").Status, Is.EqualTo(404));

    [Test]
    public void File_showsAnEditOverTheRepository()
    {
        var edits = Edits();
        edits["README.md"] = "# Edited\n";

        Assert.That(FakeFilesProvider.File(Repository(), edits, [], "README.md").Body["content"]!.GetValue<string>(), Is.EqualTo("# Edited\n"));
    }

    private static Dictionary<string, string> Edits() => new(StringComparer.Ordinal);

    private static JsonObject Repository() => new()
    {
        ["README.md"] = "# Harbor Shop\n",
        ["apps/storefront/ProductGrid.tsx"] = "export function ProductGrid() {}\n",
        ["apps/api/orders.ts"] = "export const openOrders = 12;\n",
        ["docs/old.md"] = "gone\n",
    };
}

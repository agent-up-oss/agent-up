using System.Reactive.Linq;
using AgentUp.Desktop.Features.Git.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Git.Unit;

[TestFixture]
public sealed class GitChangeNodeViewModelTests
{
    [Test]
    public void Directory_usesAMutedGlyphAndNoFileStatus()
    {
        var node = new GitChangeNodeViewModel("src", "src", 1, true, string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(node.ToggleGlyph, Is.EqualTo("▾"));
            Assert.That(node.IsDirectory, Is.True);
            Assert.That(node.IsFile, Is.False);
            Assert.That(node.IsModified, Is.False);
            Assert.That(node.IsExpanded, Is.True);
            Assert.That(node.Guides, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Directory_toggleSwitchesToTheCollapsedChevron()
    {
        var node = new GitChangeNodeViewModel("src", "src", 1, true, string.Empty);

        await node.ToggleExpandCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(node.IsExpanded, Is.False);
            Assert.That(node.IsCollapsed, Is.True);
            Assert.That(node.ToggleGlyph, Is.EqualTo("▸"));
        });
    }

    [Test]
    public void Directory_setExpandedSilentlyIsIdempotentUntilTheValueChanges()
    {
        var node = new GitChangeNodeViewModel("src", "src", 1, true, string.Empty);

        node.SetExpandedSilently(true);
        Assert.That(node.IsExpanded, Is.True);

        node.SetExpandedSilently(false);
        Assert.That(node.IsCollapsed, Is.True);
    }

    [Test]
    public async Task File_toggleExpandDoesNotCollapse()
    {
        var node = new GitChangeNodeViewModel("main.cs", "src/main.cs", 2, false, "Modified");

        await node.ToggleExpandCommand.Execute().FirstAsync();

        Assert.That(node.IsExpanded, Is.True);
    }

    [Test]
    public void TokenViewModel_exposesTheGrammarKind()
    {
        var token = new GitFileDiffTokenViewModel("keyword", "class");

        Assert.That(token.Kind, Is.EqualTo("keyword"));
        Assert.That(token.Text, Is.EqualTo("class"));
        Assert.That(token.IsKeyword, Is.True);
        Assert.That(token.IsType, Is.False);
    }

    [TestCase("Added", "+", true, false, false, false, false, false)]
    [TestCase("Untracked", "?", false, true, false, false, false, false)]
    [TestCase("Deleted", "−", false, false, true, false, false, false)]
    [TestCase("Renamed", "→", false, false, false, true, false, false)]
    [TestCase("Conflicted", "!", false, false, false, false, true, false)]
    [TestCase("Modified", "M", false, false, false, false, false, true)]
    public void File_exposesCatalogStatusKind(
        string status,
        string glyph,
        bool added,
        bool untracked,
        bool deleted,
        bool renamed,
        bool conflicted,
        bool modified)
    {
        var node = new GitChangeNodeViewModel("main.cs", "src/main.cs", 2, false, status);

        Assert.Multiple(() =>
        {
            Assert.That(node.Glyph, Is.EqualTo(glyph));
            Assert.That(node.IsAdded, Is.EqualTo(added));
            Assert.That(node.IsUntracked, Is.EqualTo(untracked));
            Assert.That(node.IsDeleted, Is.EqualTo(deleted));
            Assert.That(node.IsRenamed, Is.EqualTo(renamed));
            Assert.That(node.IsConflicted, Is.EqualTo(conflicted));
            Assert.That(node.IsModified, Is.EqualTo(modified));
        });
    }
}

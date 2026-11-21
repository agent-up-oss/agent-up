using AgentUp.Desktop.Features.Agents.Providers;

namespace AgentUp.Desktop.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentDiffProviderTests
{
    [Test]
    public void A_unified_diff_keeps_its_hunks_numbers_and_path()
    {
        var diff = AgentDiffProvider.FromUnified("Index: /repo/server.ts\n===\n--- /repo/server.ts\n+++ /repo/server.ts\n@@ -2,3 +2,3 @@\n import x\n-listen(3000)\n+listen(3001)\n end\n\\ No newline at end of file");

        Assert.That(diff.Path, Is.EqualTo("/repo/server.ts"));
        Assert.That(diff.Lines.Select(line => line.Kind), Is.EqualTo(new[] { "hunk", "context", "deleted", "added", "context" }));
        Assert.That((diff.Added, diff.Removed), Is.EqualTo((1, 1)));
        Assert.That(diff.Lines[2].OldNumber, Is.EqualTo(3));
        Assert.That(diff.Lines[3].NewNumber, Is.EqualTo(3));
        Assert.That(diff.Lines[3].Text, Is.EqualTo("listen(3001)"));
    }

    [Test]
    public void Two_texts_compare_to_the_changed_lines_with_context_and_a_gap_between_distant_changes()
    {
        var before = string.Join('\n', Enumerable.Range(1, 20).Select(index => $"line {index}"));
        var after = before.Replace("line 2\n", "line two\n", StringComparison.Ordinal).Replace("line 18", "line eighteen", StringComparison.Ordinal);

        var diff = AgentDiffProvider.FromTexts(before, after, "a.txt");

        Assert.That((diff.Added, diff.Removed), Is.EqualTo((2, 2)));
        Assert.That(diff.Lines.Count(line => line.Kind == "hunk"), Is.EqualTo(1), "Changes ten lines apart are two sections.");
        Assert.That(diff.Lines.Where(line => line.Kind == "context").Select(line => line.Text), Does.Not.Contain("line 10"));
        Assert.That(diff.Lines.First().Text, Is.EqualTo("line 1"));
    }

    [Test]
    public void A_new_file_is_every_line_added_and_huge_texts_are_a_whole_replacement()
    {
        var created = AgentDiffProvider.NewFile("a\nb\n", "new.md");
        var huge = string.Join('\n', Enumerable.Range(1, AgentDiffProvider.CompareLimit + 1));
        var replaced = AgentDiffProvider.FromTexts(huge, "x", "big.txt");

        Assert.That((created.Added, created.Removed), Is.EqualTo((2, 0)));
        Assert.That(created.Lines.Select(line => line.NewNumber), Is.EqualTo(new int?[] { 1, 2 }));
        Assert.That((replaced.Added, replaced.Removed), Is.EqualTo((1, AgentDiffProvider.CompareLimit + 1)));
    }
}

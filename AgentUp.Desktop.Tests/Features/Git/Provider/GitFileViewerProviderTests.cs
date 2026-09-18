using AgentUp.Desktop.Features.Git.Providers;

namespace AgentUp.Desktop.Tests.Features.Git.Provider;

[TestFixture]
public sealed class GitFileViewerProviderTests
{
    private const string Sample = """
        diff --git a/src/app/main.cs b/src/app/main.cs
        index 111..222 100644
        --- a/src/app/main.cs
        +++ b/src/app/main.cs
        @@ -1,3 +1,4 @@
         using System;
        -public class Old
        +public sealed class GitFileDiff
         {
        """;

    [Test]
    public void ParseDiff_numbersAddedAndDeletedLinesFromHunkHeaders()
    {
        var lines = GitFileViewerProvider.ParseDiff(Sample);

        Assert.That(lines[0].Kind, Is.EqualTo("meta"));
        var hunks = GitFileViewerProvider.Hunks(lines);
        Assert.That(hunks, Has.Count.EqualTo(1));
        var added = lines.Single(line => line.Kind == "added");
        var deleted = lines.Single(line => line.Kind == "deleted");
        Assert.That(added.NewNumber, Is.EqualTo(2));
        Assert.That(deleted.OldNumber, Is.EqualTo(2));
        Assert.That(GitFileViewerProvider.SplitDiffLines(string.Join('\n', Enumerable.Range(0, 400).Select(index => $"line {index}"))), Has.Count.EqualTo(400));
    }

    [Test]
    public void Highlight_usesTheSharedDesignSystemGrammarForThePath()
    {
        Assert.That(GitFileViewerProvider.DetectLanguage("GitChangesController.cs"), Is.EqualTo("csharp"));
        var lines = GitFileViewerProvider.ParseDiff(Sample);
        var added = lines.Single(line => line.Kind == "added");
        var tokens = GitFileViewerProvider.Highlight("src/app/main.cs", added);
        Assert.That(tokens.Any(token => token is { Kind: "keyword", Text: "class" }), Is.True);
        Assert.That(GitFileViewerProvider.JumpIndex(lines, "2"), Is.EqualTo(added.Index).Or.EqualTo(lines.Single(line => line.Kind == "deleted").Index));
    }
}

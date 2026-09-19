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
        Assert.That(GitFileViewerProvider.JumpIndex(lines, ""), Is.Null);
        Assert.That(GitFileViewerProvider.JumpIndex(lines, "@@"), Is.EqualTo(lines.Single(line => line.Kind == "hunk").Index));
        Assert.That(GitFileViewerProvider.ParseDiff(""), Is.Empty);
        Assert.That(GitFileViewerProvider.DetectLanguage(" "), Is.EqualTo("plaintext"));
        Assert.That(GitFileViewerProvider.DetectLanguage("Dockerfile"), Is.EqualTo("shell"));
        Assert.That(GitFileViewerProvider.DetectLanguage("Makefile"), Is.EqualTo("shell"));
        Assert.That(GitFileViewerProvider.DetectLanguage("LICENSE"), Is.EqualTo("plaintext"));
    }

    [Test]
    public void Tokenize_coversCommentsStringsNumbersTypesAndOperators()
    {
        Assert.That(GitFileViewerProvider.Tokenize("", "csharp"), Is.Empty);
        Assert.That(GitFileViewerProvider.Tokenize("plain", "missing-language")[0].Kind, Is.EqualTo("plain"));

        var commented = GitFileViewerProvider.Tokenize("return; // done", "csharp");
        Assert.That(commented.Any(token => token is { Kind: "comment", Text: "// done" }), Is.True);

        var block = GitFileViewerProvider.Tokenize("/* a */ int x = 0xFFn;", "csharp");
        Assert.That(block.Any(token => token.Kind == "comment" && token.Text.Contains("/*")), Is.True);
        Assert.That(block.Any(token => token is { Kind: "type", Text: "int" }), Is.True);
        Assert.That(block.Any(token => token is { Kind: "number", Text: "0xFFn" }), Is.True);

        var escaped = GitFileViewerProvider.Tokenize("""var s = "a\"b";""", "csharp");
        Assert.That(escaped.Any(token => token.Kind == "string" && token.Text.Contains("\\\"")), Is.True);

        var call = GitFileViewerProvider.Tokenize("obj.Run(1 + 2);", "csharp");
        Assert.That(call.Any(token => token is { Kind: "property", Text: "Run" } or { Kind: "function", Text: "Run" }), Is.True);
        Assert.That(call.Any(token => token.Kind == "operator"), Is.True);
        Assert.That(call.Any(token => token.Kind == "punctuation"), Is.True);
    }

    [Test]
    public void Tokenize_keepsLeadingSpacesAndTabsInDisplayedTokens()
    {
        var spaces = GitFileViewerProvider.Tokenize("    return foo;", "csharp");
        Assert.That(string.Concat(spaces.Select(token => token.Text)), Is.EqualTo("    return foo;"));
        Assert.That(spaces[0].Text, Is.EqualTo("    "));
        Assert.That(spaces.Any(token => token is { Kind: "keyword", Text: "return" }), Is.True);

        var tabbed = GitFileViewerProvider.Tokenize("\treturn foo;", "csharp");
        Assert.That(string.Concat(tabbed.Select(token => token.Text)), Is.EqualTo("\treturn foo;"));
        Assert.That(tabbed[0].Text, Is.EqualTo("\t"));
    }
}

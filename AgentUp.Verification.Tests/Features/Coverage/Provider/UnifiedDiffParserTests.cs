using AgentUp.Verification.Features.Coverage.Providers;

namespace AgentUp.Verification.Tests.Features.Coverage.Provider;

[TestFixture]
public sealed class UnifiedDiffParserTests
{
    private const string Path = "AgentUp.Server/Program.cs";

    [Test]
    public void Parse_readsASingleModifiedLineFromAHeaderWithNoCount()
    {
        // "@@ -2 +2 @@" - an omitted count means exactly one line.
        var parsed = new UnifiedDiffParser().Parse($"""
            diff --git a/{Path} b/{Path}
            --- a/{Path}
            +++ b/{Path}
            @@ -2 +2 @@
            -old
            +new
            """);

        Assert.That(parsed.Lines[Path], Is.EquivalentTo(new[] { 2 }));
    }

    [Test]
    public void Parse_expandsAMultiLineHunkToEveryAddedLine()
    {
        var parsed = new UnifiedDiffParser().Parse($"""
            +++ b/{Path}
            @@ -10,0 +10,3 @@
            +a
            +b
            +c
            """);

        Assert.That(parsed.Lines[Path], Is.EquivalentTo(new[] { 10, 11, 12 }));
    }

    [Test]
    public void Parse_ignoresAPureDeletionBecauseRemovedCodeCannotBeCovered()
    {
        var parsed = new UnifiedDiffParser().Parse($"""
            +++ b/{Path}
            @@ -5,2 +4,0 @@
            -gone
            -also gone
            """);

        Assert.That(parsed.Lines, Is.Empty);
    }

    [Test]
    public void Parse_keepsHunksFromSeveralFilesApart()
    {
        var other = "AgentUp.CLI/Program.cs";
        var parsed = new UnifiedDiffParser().Parse($"""
            +++ b/{Path}
            @@ -1 +1 @@
            +one
            +++ b/{other}
            @@ -20,0 +20,2 @@
            +two
            +three
            """);

        Assert.Multiple(() =>
        {
            Assert.That(parsed.Lines[Path], Is.EquivalentTo(new[] { 1 }));
            Assert.That(parsed.Lines[other], Is.EquivalentTo(new[] { 20, 21 }));
        });
    }

    [Test]
    public void Parse_unionsSeveralHunksInOneFile()
    {
        var parsed = new UnifiedDiffParser().Parse($"""
            +++ b/{Path}
            @@ -1 +1 @@
            +one
            @@ -30,0 +30,2 @@
            +thirty
            +thirtyone
            """);

        Assert.That(parsed.Lines[Path], Is.EquivalentTo(new[] { 1, 30, 31 }));
    }

    [Test]
    public void Parse_ignoresADeletedFileWhoseNewSideIsDevNull()
    {
        var parsed = new UnifiedDiffParser().Parse($"""
            --- a/{Path}
            +++ /dev/null
            @@ -1,3 +0,0 @@
            -a
            """);

        Assert.That(parsed.Lines, Is.Empty);
    }

    [Test]
    public void Parse_toleratesAHunkHeaderCarryingTrailingContext()
    {
        // Git appends the enclosing declaration after the closing @@.
        var parsed = new UnifiedDiffParser().Parse($"""
            +++ b/{Path}
            @@ -12 +12 @@ public sealed class Thing
            +changed
            """);

        Assert.That(parsed.Lines[Path], Is.EquivalentTo(new[] { 12 }));
    }

    [Test]
    public void Parse_returnsNothingForEmptyOutput()
    {
        Assert.That(new UnifiedDiffParser().Parse(string.Empty).Lines, Is.Empty);
    }

    [Test]
    public void Parse_ignoresHunksBeforeAnyFileHeader()
    {
        Assert.That(new UnifiedDiffParser().Parse("@@ -1 +1 @@\n+orphan\n").Lines, Is.Empty);
    }
}

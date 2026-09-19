using System.Reactive.Linq;
using AgentUp.Desktop.Features.Git.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Git.Unit;

[TestFixture]
public sealed class GitFileDiffViewModelTests
{
    private const string Sample = """
        diff --git a/src/app/main.cs b/src/app/main.cs
        --- a/src/app/main.cs
        +++ b/src/app/main.cs
        @@ -1,3 +1,4 @@
         using System;
        -public class Old
        +public sealed class GitFileDiff
         {
        """;

    [Test]
    public async Task JumpCommand_ignoresOutOfRangeIndexesThenMovesToALine()
    {
        var diff = new GitFileDiffViewModel();
        diff.ShowDiff("src/app/main.cs", "Modified", Sample);

        await diff.JumpCommand.Execute(-1).FirstAsync();
        Assert.That(diff.CurrentLine, Is.EqualTo(diff.Lines[0]));

        await diff.JumpCommand.Execute(diff.Lines.Count - 1).FirstAsync();
        Assert.That(diff.CurrentLine, Is.EqualTo(diff.Lines[^1]));
    }

    [Test]
    public async Task JumpToEnteredLine_usesTheGotoQuery()
    {
        var diff = new GitFileDiffViewModel();
        diff.ShowDiff("src/app/main.cs", "Modified", Sample);
        diff.GotoQuery = "2";

        await diff.JumpToEnteredLineCommand.Execute().FirstAsync();

        Assert.That(diff.CurrentLine, Is.Not.Null);
        Assert.That(diff.CurrentLine!.OldNumber == 2 || diff.CurrentLine.NewNumber == 2, Is.True);

        diff.GotoQuery = "";
        var current = diff.CurrentLine;
        await diff.JumpToEnteredLineCommand.Execute().FirstAsync();
        Assert.That(diff.CurrentLine, Is.EqualTo(current));
    }
}

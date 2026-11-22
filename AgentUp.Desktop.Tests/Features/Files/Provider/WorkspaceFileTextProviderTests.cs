using AgentUp.Desktop.Features.Files.Providers;

namespace AgentUp.Desktop.Tests.Features.Files.Provider;

[TestFixture]
public sealed class WorkspaceFileTextProviderTests
{
    [TestCase("", 1)]
    [TestCase("one", 1)]
    [TestCase("one\ntwo\n", 3)]
    [TestCase("one\r\ntwo", 2)]
    public void LineCount_countsLinesTheWayTheGutterNumbersThem(string text, int lines)
        => Assert.That(WorkspaceFileTextProvider.LineCount(text), Is.EqualTo(lines));

    [Test]
    public void LineNumbers_isOneNumberPerLine()
        => Assert.That(WorkspaceFileTextProvider.LineNumbers(3), Is.EqualTo("1\n2\n3"));

    [TestCase(0, "Ln 1, Col 1")]
    [TestCase(3, "Ln 1, Col 4")]
    [TestCase(4, "Ln 2, Col 1")]
    [TestCase(99, "Ln 2, Col 4")]
    public void CaretLabel_namesTheLineAndColumn(int caret, string label)
        => Assert.That(WorkspaceFileTextProvider.CaretLabel("one\ntwo", caret), Is.EqualTo(label));

    [TestCase(null, "")]
    [TestCase(612L, "612 B")]
    [TestCase(1843L, "1.8 KB")]
    [TestCase(3_145_728L, "3 MB")]
    public void SizeLabel_readsAsAPersonWouldSayIt(long? bytes, string label)
        => Assert.That(WorkspaceFileTextProvider.SizeLabel(bytes), Is.EqualTo(label));

    [TestCase(null, "")]
    [TestCase("Modified", "M")]
    [TestCase("Added", "+")]
    [TestCase("Untracked", "?")]
    [TestCase("Renamed", "→")]
    [TestCase("Conflicted", "!")]
    public void StatusGlyph_matchesTheGitChangeList(string? status, string glyph)
        => Assert.That(WorkspaceFileTextProvider.StatusGlyph(status), Is.EqualTo(glyph));

    [Test]
    public void StatusLabel_saysUnchangedForAFileGitDoesNotList()
        => Assert.That(WorkspaceFileTextProvider.StatusLabel(null), Is.EqualTo("Unchanged"));
}

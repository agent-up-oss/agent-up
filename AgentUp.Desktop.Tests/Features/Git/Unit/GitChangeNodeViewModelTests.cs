using AgentUp.Desktop.Features.Git.ViewModels;
using AgentUp.Desktop.Shared.Models;

namespace AgentUp.Desktop.Tests.Features.Git.Unit;

[TestFixture]
public sealed class GitChangeNodeViewModelTests
{
    [Test]
    public void Directory_usesMutedGlyphAndNameColors()
    {
        var node = new GitChangeNodeViewModel("src", "src", 1, true, string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(node.GlyphColor, Is.EqualTo(AgentUpThemeColors.TextMuted));
            Assert.That(node.NameColor, Is.EqualTo(AgentUpThemeColors.TextMuted));
        });
    }

    [TestCase("Added", AgentUpThemeColors.AccentSoft)]
    [TestCase("Untracked", AgentUpThemeColors.AccentSoft)]
    [TestCase("Deleted", AgentUpThemeColors.StatusDanger)]
    [TestCase("Renamed", AgentUpThemeColors.StatusInfo)]
    [TestCase("Conflicted", AgentUpThemeColors.StatusWarning)]
    [TestCase("Modified", AgentUpThemeColors.TextSecondary)]
    public void File_usesStatusColorForGlyph(string status, string color)
    {
        var node = new GitChangeNodeViewModel("main.cs", "src/main.cs", 2, false, status);

        Assert.Multiple(() =>
        {
            Assert.That(node.GlyphColor, Is.EqualTo(color));
            Assert.That(node.NameColor, Is.EqualTo(AgentUpThemeColors.TextPrimary));
        });
    }
}

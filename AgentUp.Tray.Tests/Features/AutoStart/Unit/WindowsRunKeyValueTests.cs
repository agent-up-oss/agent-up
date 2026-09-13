using AgentUp.Tray.Features.AutoStart;

namespace AgentUp.Tray.Tests.Features.AutoStart.Unit;

[TestFixture]
public sealed class WindowsRunKeyValueTests
{
    private const string ExePath = @"C:\Program Files\Agent-Up\AgentUp.Tray.exe";

    [Test]
    public void Format_quotesThePathSoASpaceIsNotReadAsAnArgument()
    {
        Assert.That(WindowsRunKeyValue.Format(ExePath), Is.EqualTo($"\"{ExePath}\""));
    }

    [Test]
    public void Matches_acceptsTheValueItWouldHaveWritten()
    {
        Assert.That(WindowsRunKeyValue.Matches(WindowsRunKeyValue.Format(ExePath), ExePath),
            Is.True);
    }

    [Test]
    public void Matches_ignoresPathCasingBecauseWindowsPathsAreCaseInsensitive()
    {
        Assert.That(WindowsRunKeyValue.Matches($"\"{ExePath.ToUpperInvariant()}\"", ExePath), Is.True);
    }

    [Test]
    public void Matches_rejectsAnUnquotedValueSoAnOlderEntryIsRewritten()
    {
        Assert.That(WindowsRunKeyValue.Matches(ExePath, ExePath), Is.False);
    }

    [Test]
    public void Matches_rejectsAValueForADifferentInstallation()
    {
        Assert.That(WindowsRunKeyValue.Matches("\"D:\\\\Old\\\\AgentUp.Tray.exe\"", ExePath), Is.False);
    }

    [Test]
    public void Matches_rejectsAnAbsentValue()
    {
        Assert.That(WindowsRunKeyValue.Matches(null, ExePath), Is.False);
    }
}

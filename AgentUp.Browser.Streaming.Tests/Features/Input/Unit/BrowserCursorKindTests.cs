using AgentUp.Browser.Streaming.Models;

namespace AgentUp.Browser.Streaming.Tests.Features.Input.Unit;

[TestFixture]
public sealed class BrowserCursorKindTests
{
    [TestCase("pointer", "pointer")]
    [TestCase("text", "text")]
    [TestCase("vertical-text", "text")]
    [TestCase("grab", "grab")]
    [TestCase("grabbing", "grab")]
    public void From_mapsCssCursorsOntoTheSetTheClientRenders(string css, string expected)
    {
        Assert.That(BrowserCursorKind.From(css), Is.EqualTo(expected));
    }

    [TestCase("auto")]
    [TestCase("crosshair")]
    [TestCase("")]
    [TestCase(null)]
    public void From_fallsBackToDefaultForAnythingElse(string? css)
    {
        Assert.That(BrowserCursorKind.From(css), Is.EqualTo(BrowserCursorKind.Default));
    }
}

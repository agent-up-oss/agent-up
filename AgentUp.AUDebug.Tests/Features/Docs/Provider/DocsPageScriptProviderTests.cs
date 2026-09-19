using AgentUp.AUDebug.Features.Docs.Providers;

namespace AgentUp.AUDebug.Tests.Features.Docs.Provider;

[TestFixture]
public sealed class DocsPageScriptProviderTests
{
    [Test]
    public void Heading_embedsTheRequestedText()
    {
        var script = DocsHeadingScriptProvider.Build("What it is");

        Assert.That(script, Does.Contain("\"What it is\""));
        Assert.That(script, Does.Contain("scrollIntoView"));
        Assert.That(script, Does.Contain("window.scrollBy"));
        Assert.That(script, Does.Contain("Heading not found"));
        Assert.That(script, Does.Contain("getElementById"));
    }

    [Test]
    public void Heading_jsonEncodesQuotes()
    {
        var script = DocsHeadingScriptProvider.Build("It's \"quoted\"");
        Assert.That(script, Does.Contain("It\\u0027s \\u0022quoted\\u0022"));
    }

    [Test]
    public void Ready_waitsForTheDocument()
    {
        var script = DocsPageReadyScriptProvider.Build("http://127.0.0.1:10100/docs/");
        Assert.That(script, Does.Contain("127.0.0.1:10100"));
        Assert.That(script, Does.Contain("Timed out waiting for the docs page to render"));
        Assert.That(script, Does.Contain("/docs/"));
    }

    [Test]
    public void Size_readsDocumentScrollBox()
    {
        var script = DocsPageSizeScriptProvider.Build();
        Assert.That(script, Does.Contain("scrollWidth"));
        Assert.That(script, Does.Contain("scrollHeight"));
        Assert.That(script, Does.Contain("1440"));
    }
}

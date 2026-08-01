using AgentUp.Server.Features.Browser.Services;

namespace AgentUp.Server.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class BrowserWorkspaceIdParserTests
{
    [Test]
    public void Parse_TrimsAndDropsEmptyWorkspaceIds()
    {
        var parser = new BrowserWorkspaceIdParser();

        var ids = parser.Parse(" one, ,two ");

        Assert.That(ids, Is.EqualTo(new[] { "one", "two" }));
    }
}

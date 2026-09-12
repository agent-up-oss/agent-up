using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Mobile.Providers;

namespace AgentUp.AUDebug.Tests.Features.Mobile.Provider;

[TestFixture]
public sealed class MobileLoginScriptProviderTests
{
    [Test]
    public void Build_embedsServerUrlAndPassword()
    {
        var script = MobileLoginScriptProvider.Build("http://localhost:5001", "test");

        Assert.That(script, Does.Contain("http://localhost:5001"));
        Assert.That(script, Does.Contain("Try and save"));
        Assert.That(script, Does.Contain("Sign in"));
        Assert.That(script, Does.Contain("\"test\""));
        Assert.That(script, Does.Contain("insertText"));
        Assert.That(script, Does.Contain("waitEnabled"));
    }

    [Test]
    public void DebuggerList_readsWebsocketUrl()
    {
        var json = """[{"id":"1","webSocketDebuggerUrl":"ws://127.0.0.1:19222/devtools/page/1"}]""";
        Assert.That(ChromiumDebuggerListParser.ReadWebSocketUrl(json), Is.EqualTo("ws://127.0.0.1:19222/devtools/page/1"));
    }

    [Test]
    public void DebuggerList_empty_returnsNull()
    {
        Assert.That(ChromiumDebuggerListParser.ReadWebSocketUrl("[]"), Is.Null);
    }
}

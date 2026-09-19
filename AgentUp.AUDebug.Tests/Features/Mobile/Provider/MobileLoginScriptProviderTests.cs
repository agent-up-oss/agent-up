using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Mobile.Providers;
using AgentUp.AUDebug.Shared.Providers;

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
        Assert.That(script, Does.Contain("waitSignIn"));
        Assert.That(script, Does.Contain("\"test\""));
        Assert.That(script, Does.Contain("insertText"));
        Assert.That(script, Does.Contain("waitEnabled"));
    }

    [Test]
    public void OpenAgentScript_clicksOpenChat()
    {
        var script = MobileOpenAgentScriptProvider.Build();
        Assert.That(script, Does.Contain("Open chat"));
        Assert.That(script, Does.Contain("waitForText"));
    }

    [Test]
    public void DebuggerList_prefersAMatchingPageUrl()
    {
        var json = """
            [
              {"id":"1","url":"about:blank","webSocketDebuggerUrl":"ws://127.0.0.1:19223/devtools/page/blank"},
              {"id":"2","url":"http://127.0.0.1:10100/docs/workspaces","webSocketDebuggerUrl":"ws://127.0.0.1:19223/devtools/page/docs"}
            ]
            """;
        Assert.That(
            ChromiumDebuggerListParser.ReadWebSocketUrl(json, "127.0.0.1:10100"),
            Is.EqualTo("ws://127.0.0.1:19223/devtools/page/docs"));
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

    [Test]
    public void DebuggerList_skipsEntriesWithoutAUsableWebsocket()
    {
        var json = """
            [
              "skip",
              {"id":"1"},
              {"id":"2","webSocketDebuggerUrl":""},
              {"id":"3","webSocketDebuggerUrl":"ws://127.0.0.1:19222/devtools/page/3"}
            ]
            """;
        Assert.That(
            ChromiumDebuggerListParser.ReadWebSocketUrl(json),
            Is.EqualTo("ws://127.0.0.1:19222/devtools/page/3"));
    }
}

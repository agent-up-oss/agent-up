using System.Text;
using AgentUp.Server.Features.Authentication.Providers;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class WebSocketAuthenticationProtocolTests
{
    [Test]
    public void ReadTokenDecodesSelectedBase64UrlProtocol()
    {
        var token = "token+/= with unicode £";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(token))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var result = WebSocketAuthenticationProtocol.ReadToken(
            true,
            $"unrelated, {WebSocketAuthenticationProtocol.Prefix}{encoded}");

        Assert.That(result, Is.EqualTo(token));
    }

    [TestCase(false, "agent-up.auth.dG9rZW4=")]
    [TestCase(true, "unrelated")]
    [TestCase(true, "agent-up.auth.!")]
    public void ReadTokenRejectsInapplicableOrMalformedProtocols(bool isWebSocket, string protocols)
    {
        Assert.That(WebSocketAuthenticationProtocol.ReadToken(isWebSocket, protocols), Is.Null);
    }
}

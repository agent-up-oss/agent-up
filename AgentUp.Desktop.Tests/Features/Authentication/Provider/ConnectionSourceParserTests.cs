using AgentUp.Desktop.Features.Authentication.DTOs;
using AgentUp.Desktop.Features.Authentication.Models;
using AgentUp.Desktop.Features.Authentication.Providers;

namespace AgentUp.Desktop.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class ConnectionSourceParserTests
{
    [Test]
    public void Parse_readsACurrentServerDocument()
    {
        var source = ConnectionSourceParser.Parse("http://127.0.0.1:5000", Document());

        Assert.Multiple(() =>
        {
            Assert.That(source.Id, Is.EqualTo("local"));
            Assert.That(source.AuthMode, Is.EqualTo("localAdministrator"));
            Assert.That(source.IsLegacy, Is.False);
            Assert.That(ConnectionSourceParser.SignInSurface(source.AuthMode), Is.EqualTo(ConnectionSignInSurface.Password));
        });
    }

    [Test]
    public void Parse_rejectsUnknownApiVersionKindAndMode()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () => ConnectionSourceParser.Parse("http://127.0.0.1:5000", Document(apiVersion: "2")),
                Throws.InvalidOperationException.With.Message.Contains("unknown apiVersion '2'"));
            Assert.That(
                () => ConnectionSourceParser.Parse("http://127.0.0.1:5000", Document(kind: "hosted")),
                Throws.InvalidOperationException.With.Message.Contains("unknown kind 'hosted'"));
            Assert.That(
                () => ConnectionSourceParser.Parse("http://127.0.0.1:5000", Document(mode: "magicLink")),
                Throws.InvalidOperationException.With.Message.Contains("unknown authentication.mode 'magicLink'"));
        });
    }

    [Test]
    public void LegacySelfHosted_isOnlySelfHostedAfterAuthStatus()
    {
        var required = ConnectionSourceParser.LegacySelfHosted("http://127.0.0.1:5000", true);
        var open = ConnectionSourceParser.LegacySelfHosted("http://127.0.0.1:5000", false);

        Assert.Multiple(() =>
        {
            Assert.That(required.Kind, Is.EqualTo("selfHosted"));
            Assert.That(required.AuthMode, Is.EqualTo("localAdministrator"));
            Assert.That(required.IsLegacy, Is.True);
            Assert.That(open.AuthMode, Is.EqualTo("disabled"));
        });
    }

    private static ConnectionMetadataResponse Document(
        string apiVersion = "1",
        string kind = "selfHosted",
        string mode = "localAdministrator")
        => new(
            apiVersion,
            "local",
            kind,
            "Agent-Up",
            new ConnectionAuthenticationResponse(mode, "Enter the administrator password to continue.", false),
            "serverScoped");
}

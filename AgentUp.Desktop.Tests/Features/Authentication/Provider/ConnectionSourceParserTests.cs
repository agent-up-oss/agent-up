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
            Assert.That(source.Kind, Is.EqualTo("selfHosted"));
            Assert.That(source.BaseUrl, Is.EqualTo("http://127.0.0.1:5000"));
            Assert.That(source.DisplayName, Is.EqualTo("Agent-Up"));
            Assert.That(source.AuthMode, Is.EqualTo("localAdministrator"));
            Assert.That(source.ApiVersion, Is.EqualTo("1"));
            Assert.That(source.WorkspacePresentation, Is.EqualTo("serverScoped"));
            Assert.That(source.Prompt, Is.EqualTo("Enter the administrator password to continue."));
            Assert.That(source.IdentifierRequired, Is.False);
            Assert.That(source.IsLegacy, Is.False);
        });
    }

    [Test]
    public void SignInSurface_mapsKnownModesAndRejectsUnknown()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ConnectionSourceParser.SignInSurface("disabled"), Is.EqualTo(ConnectionSignInSurface.None));
            Assert.That(ConnectionSourceParser.SignInSurface("localAdministrator"), Is.EqualTo(ConnectionSignInSurface.Password));
            Assert.That(ConnectionSourceParser.SignInSurface("browserSso"), Is.EqualTo(ConnectionSignInSurface.BrowserSso));
            Assert.That(ConnectionSourceParser.SignInSurface("externalBearer"), Is.EqualTo(ConnectionSignInSurface.ExternalBearer));
            Assert.That(
                () => ConnectionSourceParser.SignInSurface("magicLink"),
                Throws.InvalidOperationException.With.Message.Contains("unknown authentication.mode 'magicLink'"));
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
    public void Parse_rejectsUnknownWorkspacePresentationAndMissingAuthentication()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                () => ConnectionSourceParser.Parse(
                    "http://127.0.0.1:5000",
                    new ConnectionMetadataResponse(
                        "1",
                        "local",
                        "selfHosted",
                        "Agent-Up",
                        new ConnectionAuthenticationResponse(
                            "localAdministrator",
                            "Enter the administrator password to continue.",
                            true),
                        "workspaceScoped")),
                Throws.InvalidOperationException.With.Message.Contains("unknown workspacePresentation 'workspaceScoped'"));
            Assert.That(
                () => ConnectionSourceParser.Parse(
                    "http://127.0.0.1:5000",
                    new ConnectionMetadataResponse("1", "local", "selfHosted", "Agent-Up", null, "serverScoped")),
                Throws.InvalidOperationException.With.Message.Contains("did not return a connection authentication document"));
            Assert.That(
                () => ConnectionSourceParser.Parse(
                    "http://127.0.0.1:5000",
                    Document(prompt: " ")),
                Throws.InvalidOperationException.With.Message.Contains("did not return a connection authentication.prompt"));
        });
    }

    [Test]
    public void LegacySelfHosted_isOnlySelfHostedAfterAuthStatus()
    {
        var required = ConnectionSourceParser.LegacySelfHosted("http://127.0.0.1:5000", true);
        var open = ConnectionSourceParser.LegacySelfHosted("not-a-url", false);

        Assert.Multiple(() =>
        {
            Assert.That(required.Kind, Is.EqualTo("selfHosted"));
            Assert.That(required.AuthMode, Is.EqualTo("localAdministrator"));
            Assert.That(required.IsLegacy, Is.True);
            Assert.That(open.AuthMode, Is.EqualTo("disabled"));
            Assert.That(open.DisplayName, Is.EqualTo("Agent-Up Server"));
            Assert.That(
                ConnectionSourceParser.SignInSurface(required.AuthMode),
                Is.EqualTo(ConnectionSignInSurface.Password));
        });
    }

    private static ConnectionMetadataResponse Document(
        string apiVersion = "1",
        string kind = "selfHosted",
        string mode = "localAdministrator",
        string prompt = "Enter the administrator password to continue.")
        => new(
            apiVersion,
            "local",
            kind,
            "Agent-Up",
            new ConnectionAuthenticationResponse(mode, prompt, false),
            "serverScoped");
}

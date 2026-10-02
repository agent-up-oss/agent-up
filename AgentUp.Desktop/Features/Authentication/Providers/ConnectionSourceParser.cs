using AgentUp.Desktop.Features.Authentication.DTOs;
using AgentUp.Desktop.Features.Authentication.Models;

namespace AgentUp.Desktop.Features.Authentication.Providers;

public static class ConnectionSourceParser
{
    public const string ApiVersion = "1";
    public const string KindSelfHosted = "selfHosted";
    public const string WorkspacePresentation = "serverScoped";
    public const string AuthLocalAdministrator = "localAdministrator";
    public const string AuthExternalBearer = "externalBearer";
    public const string AuthBrowserSso = "browserSso";
    public const string AuthDisabled = "disabled";

    public static readonly string[] AuthModes =
    [
        AuthLocalAdministrator,
        AuthExternalBearer,
        AuthBrowserSso,
        AuthDisabled
    ];

    public static ConnectionSource Parse(string baseUrl, ConnectionMetadataResponse document)
    {
        var apiVersion = Required(document.ApiVersion, "apiVersion");
        if (apiVersion != ApiVersion)
            throw Unknown("apiVersion", apiVersion);

        var kind = Required(document.Kind, "kind");
        if (kind != KindSelfHosted)
            throw Unknown("kind", kind);

        var presentation = Required(document.WorkspacePresentation, "workspacePresentation");
        if (presentation != WorkspacePresentation)
            throw Unknown("workspacePresentation", presentation);

        var authentication = document.Authentication
            ?? throw new InvalidOperationException("This Server did not return a connection authentication document.");
        var mode = Required(authentication.Mode, "authentication.mode");
        if (!AuthModes.Contains(mode, StringComparer.Ordinal))
            throw Unknown("authentication.mode", mode);

        return new ConnectionSource(
            Required(document.ConnectionId, "connectionId"),
            KindSelfHosted,
            baseUrl,
            Required(document.DisplayName, "displayName"),
            mode,
            ApiVersion,
            WorkspacePresentation,
            Required(authentication.Prompt, "authentication.prompt"),
            authentication.IdentifierRequired,
            IsLegacy: false);
    }

    public static ConnectionSource LegacySelfHosted(string baseUrl, bool authenticationRequired)
    {
        var mode = authenticationRequired ? AuthLocalAdministrator : AuthDisabled;
        return new ConnectionSource(
            "legacy",
            KindSelfHosted,
            baseUrl,
            DisplayNameFromUrl(baseUrl),
            mode,
            ApiVersion,
            WorkspacePresentation,
            authenticationRequired
                ? "Enter the administrator password to continue."
                : "Authentication is not required for this Server.",
            IdentifierRequired: false,
            IsLegacy: true);
    }

    public static ConnectionSignInSurface SignInSurface(string authMode)
        => authMode switch
        {
            AuthDisabled => ConnectionSignInSurface.None,
            AuthLocalAdministrator => ConnectionSignInSurface.Password,
            AuthBrowserSso => ConnectionSignInSurface.BrowserSso,
            AuthExternalBearer => ConnectionSignInSurface.ExternalBearer,
            _ => throw Unknown("authentication.mode", authMode)
        };

    private static string Required(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"This Server did not return a connection {field}.");
        return value;
    }

    private static InvalidOperationException Unknown(string field, string value)
        => new($"This Server reported an unknown {field} '{value}'.");

    private static string DisplayNameFromUrl(string baseUrl)
        => Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host)
            ? uri.Host
            : "Agent-Up Server";
}

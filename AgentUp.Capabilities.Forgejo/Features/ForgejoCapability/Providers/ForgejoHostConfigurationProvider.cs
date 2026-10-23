using AgentUp.Sdk.Forge;

namespace AgentUp.Capabilities.Forgejo.Features.ForgejoCapability.Providers;

/// <summary>
/// Resolves Forgejo API and OAuth endpoints from module configuration.
/// <c>AGENTUP_FORGEJO_ORIGIN</c> is the instance root; API and OAuth URLs are derived
/// unless overridden. There is no github.com default.
/// </summary>
public sealed class ForgejoHostConfigurationProvider
{
    public const string DefaultOrigin = "http://localhost:3000";

    public ForgeHostConfiguration Read()
        => From(
            Environment.GetEnvironmentVariable("AGENTUP_FORGEJO_ORIGIN"),
            Environment.GetEnvironmentVariable("AGENTUP_FORGEJO_API_URL"),
            Environment.GetEnvironmentVariable("AGENTUP_FORGEJO_AUTHORIZE_URL"),
            Environment.GetEnvironmentVariable("AGENTUP_FORGEJO_TOKEN_URL"));

    public static ForgeHostConfiguration From(
        string? origin,
        string? apiBaseUrl,
        string? authorizeUrl,
        string? tokenUrl)
    {
        var host = Origin(origin);
        return new(
            ApiBase(apiBaseUrl, host),
            Coalesce(authorizeUrl, host + "/login/oauth/authorize").TrimEnd('/'),
            Coalesce(tokenUrl, host + "/login/oauth/access_token").TrimEnd('/'));
    }

    public static string ApiBase(string? apiBaseUrl, string? origin = null)
    {
        var text = string.IsNullOrWhiteSpace(apiBaseUrl)
            ? Origin(origin) + "/api/v1/"
            : apiBaseUrl.Trim();
        return text.EndsWith('/') ? text : text + "/";
    }

    public static string Origin(string? origin)
        => string.IsNullOrWhiteSpace(origin) ? DefaultOrigin : origin.Trim().TrimEnd('/');

    private static string Coalesce(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

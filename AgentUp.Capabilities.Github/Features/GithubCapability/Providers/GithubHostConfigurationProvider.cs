using AgentUp.Sdk.Forge;

namespace AgentUp.Capabilities.Github.Features.GithubCapability.Providers;

/// <summary>
/// Resolves GitHub API and OAuth endpoints from module configuration, defaulting to
/// github.com so GitHub Enterprise is a URL change rather than a code change.
/// </summary>
public sealed class GithubHostConfigurationProvider
{
    public const string DefaultApiBaseUrl = "https://api.github.com/";
    public const string DefaultAuthorizeUrl = "https://github.com/login/oauth/authorize";
    public const string DefaultTokenUrl = "https://github.com/login/oauth/access_token";

    public ForgeHostConfiguration Read()
        => From(
            Environment.GetEnvironmentVariable("AGENTUP_GITHUB_API_URL"),
            Environment.GetEnvironmentVariable("AGENTUP_GITHUB_AUTHORIZE_URL"),
            Environment.GetEnvironmentVariable("AGENTUP_GITHUB_TOKEN_URL"));

    public static ForgeHostConfiguration From(string? apiBaseUrl, string? authorizeUrl, string? tokenUrl)
        => new(
            ApiBase(apiBaseUrl),
            Coalesce(authorizeUrl, DefaultAuthorizeUrl).TrimEnd('/'),
            Coalesce(tokenUrl, DefaultTokenUrl).TrimEnd('/'));

    public static string ApiBase(string? apiBaseUrl)
    {
        var text = Coalesce(apiBaseUrl, DefaultApiBaseUrl);
        return text.EndsWith('/') ? text : text + "/";
    }

    /// <summary>
    /// Maps a user-facing GitHub URL to the REST API base. github.com becomes api.github.com;
    /// a GitHub Enterprise origin becomes <c>{origin}/api/v3/</c>. An API URL is kept as the API.
    /// </summary>
    public static string ApiBaseFromEndpoint(string endpoint)
    {
        var uri = new Uri(endpoint.Trim());
        var host = uri.Host;
        if (host.Equals("api.github.com", StringComparison.OrdinalIgnoreCase)
            || uri.AbsolutePath.Contains("/api/v3", StringComparison.OrdinalIgnoreCase))
            return ApiBase(uri.GetLeftPart(UriPartial.Path));
        if (host.Equals("github.com", StringComparison.OrdinalIgnoreCase)
            || host.Equals("www.github.com", StringComparison.OrdinalIgnoreCase))
            return DefaultApiBaseUrl;
        return ApiBase(uri.GetLeftPart(UriPartial.Authority) + "/api/v3");
    }

    private static string Coalesce(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}

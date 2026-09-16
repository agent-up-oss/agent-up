using System.Net;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Replays an intercepted OAuth redirect against the loopback listener the agent CLI opened.
/// <para>
/// The CLI binds its callback to loopback on the Server host, but the browser that finishes the
/// sign-in is on the user's desktop or phone, so that redirect never arrives on its own. The
/// client catches it and posts it here instead.
/// </para>
/// <para>
/// The URL comes from a client, so it is a request-forgery vector and is not trusted: it is
/// accepted only when it matches the redirect URI the CLI itself advertised, host, port, and
/// path included, and only when that address is loopback.
/// </para>
/// </summary>
public sealed class AgentLoginCallbackRelay(IHttpClientFactory clients, ILogger<AgentLoginCallbackRelay> logger)
{
    public async Task<bool> RelayAsync(string expectedRedirectUri, string callbackUrl, CancellationToken cancellationToken)
    {
        if (!TryAccept(expectedRedirectUri, callbackUrl, out var target))
            return false;

        using var client = clients.CreateClient("agent-login-callback");
        client.Timeout = TimeSpan.FromSeconds(15);
        try
        {
            using var response = await client.GetAsync(target, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            // The CLI often stops listening the instant it has the code, so a refused or
            // truncated connection still means the callback landed.
            logger.LogInformation(exception, "The agent CLI closed its callback listener while the redirect was relayed.");
            return true;
        }
    }

    internal static bool TryAccept(string expectedRedirectUri, string callbackUrl, out Uri target)
    {
        target = null!;
        if (!Uri.TryCreate(expectedRedirectUri, UriKind.Absolute, out var expected))
            return false;
        if (!Uri.TryCreate(callbackUrl, UriKind.Absolute, out var candidate))
            return false;
        if (candidate.Scheme != Uri.UriSchemeHttp && candidate.Scheme != Uri.UriSchemeHttps)
            return false;
        if (!IsLoopback(expected) || !IsLoopback(candidate))
            return false;
        if (expected.Port != candidate.Port)
            return false;
        if (!string.Equals(expected.AbsolutePath, candidate.AbsolutePath, StringComparison.Ordinal))
            return false;

        // Keep the CLI's own host spelling: it bound one of 127.0.0.1 or localhost, and some
        // listeners only answer on the exact prefix they registered.
        target = new UriBuilder(candidate) { Host = expected.Host }.Uri;
        return true;
    }

    private static bool IsLoopback(Uri uri) =>
        uri.IsLoopback
        || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
        || (IPAddress.TryParse(uri.Host, out var address) && IPAddress.IsLoopback(address));
}

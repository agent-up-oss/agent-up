namespace AgentUp.CLI.Shared.Providers;

public static class SecureServerUrlProvider
{
    public static Uri ResolveServerUri(string serverUrl)
    {
        if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("Server URL must be an absolute http or https URL.");

        EnsureCredentialTransportAllowed(uri);
        return uri;
    }

    public static void EnsureCredentialTransportAllowed(Uri serverUri)
    {
        if (serverUri.Scheme == Uri.UriSchemeHttps)
            return;

        if (serverUri.Scheme == Uri.UriSchemeHttp && IsLoopback(serverUri))
            return;

        throw new InvalidOperationException("HTTPS is required for remote Agent-Up server URLs.");
    }

    private static bool IsLoopback(Uri uri)
        => uri.IsLoopback
           || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
}

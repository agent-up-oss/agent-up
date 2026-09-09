namespace AgentUp.Desktop.Features.Authentication.Providers;

public static class SecureServerUrlProvider
{
    public static Uri ResolveServerUri(string? serverUrl = null)
    {
        var value = serverUrl ?? Environment.GetEnvironmentVariable("AGENTUP_SERVER_URL") ?? "http://localhost:5000";
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            throw new InvalidOperationException("AGENTUP_SERVER_URL must be an absolute http or https URL.");

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

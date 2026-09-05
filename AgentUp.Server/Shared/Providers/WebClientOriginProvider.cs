namespace AgentUp.Server.Shared.Providers;

public static class WebClientOriginProvider
{
    public const string PolicyName = "WebClients";

    public static bool IsAllowed(string origin)
        => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
           && uri.Scheme is "http" or "https";
}

namespace AgentUp.Server.Shared.Providers;

public static class LoopbackClientOriginProvider
{
    public const string PolicyName = "LoopbackWebClients";

    public static bool IsAllowed(string origin)
        => Uri.TryCreate(origin, UriKind.Absolute, out var uri)
           && uri.Scheme is "http" or "https"
           && uri.IsLoopback;
}

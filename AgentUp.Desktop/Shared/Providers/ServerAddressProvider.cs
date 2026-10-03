namespace AgentUp.Desktop.Shared.Providers;

public static class ServerAddressProvider
{
    public static bool IsLoopback(Uri uri)
        => uri.IsLoopback
           || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase);
}

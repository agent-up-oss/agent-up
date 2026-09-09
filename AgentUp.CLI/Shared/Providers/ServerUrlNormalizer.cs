namespace AgentUp.CLI.Shared.Providers;

public static class ServerUrlNormalizer
{
    public static string Normalize(string serverUrl)
    {
        var normalized = serverUrl.Trim();
        return normalized.EndsWith('/') ? normalized[..^1] : normalized;
    }
}

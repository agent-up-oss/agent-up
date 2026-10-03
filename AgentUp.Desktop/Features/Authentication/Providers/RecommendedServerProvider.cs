using AgentUp.Desktop.Features.Authentication.Models;

namespace AgentUp.Desktop.Features.Authentication.Providers;

public static class RecommendedServerProvider
{
    public static RecommendedServer? Read(IReadOnlyDictionary<string, string?>? env = null)
    {
        var url = ReadEnv(env, "AGENTUP_RECOMMENDED_SERVER_URL")
            ?? ReadEnv(env, "EXPO_PUBLIC_RECOMMENDED_SERVER_URL");
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            var name = ReadEnv(env, "AGENTUP_RECOMMENDED_SERVER_NAME")
                ?? ReadEnv(env, "EXPO_PUBLIC_RECOMMENDED_SERVER_NAME");
            return new RecommendedServer(
                RecommendedServer.RecommendedId,
                SecureServerUrlProvider.Normalize(url),
                string.IsNullOrWhiteSpace(name) ? RecommendedServer.DefaultDisplayName : name.Trim());
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static string? ReadEnv(IReadOnlyDictionary<string, string?>? env, string key)
    {
        if (env is not null)
            return env.TryGetValue(key, out var value) ? value : null;
        return Environment.GetEnvironmentVariable(key);
    }
}

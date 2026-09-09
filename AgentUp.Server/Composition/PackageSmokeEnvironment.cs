namespace AgentUp.Server.Composition;

internal static class PackageSmokeEnvironment
{
    internal const string AuthDisabledVariable = "AGENTUP_PACKAGE_SMOKE_AUTH_DISABLED";

    internal static IReadOnlyDictionary<string, string> ServerEnvironmentVariables =>
        string.Equals(Environment.GetEnvironmentVariable(AuthDisabledVariable), "true", StringComparison.OrdinalIgnoreCase)
            ? new Dictionary<string, string> { ["AGENTUP_AUTH_DISABLED"] = "true" }
            : new Dictionary<string, string>();
}

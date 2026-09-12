namespace AgentUp.Server.Composition;

internal static class PackageSmokeEnvironment
{
    internal const string AuthDisabledVariable = "AGENTUP_PACKAGE_SMOKE_AUTH_DISABLED";
    internal const string SentryDsnServerVariable = "SENTRY_DSN_SERVER";
    internal const string SentryDsnVariable = "SENTRY_DSN";

    internal static IReadOnlyDictionary<string, string> ServerEnvironmentVariables
    {
        get
        {
            var variables = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.Equals(Environment.GetEnvironmentVariable(AuthDisabledVariable), "true", StringComparison.OrdinalIgnoreCase))
                variables["AGENTUP_AUTH_DISABLED"] = "true";

            var sentryDsn = Environment.GetEnvironmentVariable(SentryDsnServerVariable);
            if (!string.IsNullOrWhiteSpace(sentryDsn))
                variables[SentryDsnVariable] = sentryDsn;

            return variables;
        }
    }
}

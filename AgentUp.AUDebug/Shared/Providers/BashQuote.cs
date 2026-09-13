namespace AgentUp.AUDebug.Shared.Providers;

public static class BashQuote
{
    public static string Single(string value)
        => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";
}

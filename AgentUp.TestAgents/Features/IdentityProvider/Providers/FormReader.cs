namespace AgentUp.TestAgents.Features.IdentityProvider.Providers;

/// <summary>
/// Reads <c>application/x-www-form-urlencoded</c> payloads and query strings, which is all the
/// wire format an OAuth deployment needs on the way in.
/// </summary>
public static class FormReader
{
    public static IReadOnlyDictionary<string, string> Query(string? query) =>
        Parse((query ?? string.Empty).TrimStart('?'));

    public static IReadOnlyDictionary<string, string> Parse(string encoded) =>
        encoded
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .GroupBy(parts => Uri.UnescapeDataString(parts[0]), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : string.Empty)
                    .First(),
                StringComparer.Ordinal);
}

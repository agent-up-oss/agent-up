namespace AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

internal sealed class StringComparerTuple : IEqualityComparer<(string, string, string)>
{
    public static StringComparerTuple Instance { get; } = new();

    public bool Equals((string, string, string) x, (string, string, string) y)
        => string.Equals(x.Item1, y.Item1, StringComparison.OrdinalIgnoreCase)
           && string.Equals(x.Item2, y.Item2, StringComparison.OrdinalIgnoreCase)
           && string.Equals(x.Item3, y.Item3, StringComparison.OrdinalIgnoreCase);

    public int GetHashCode((string, string, string) obj)
        => HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item1),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item2),
            StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Item3));
}

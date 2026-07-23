namespace AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Providers;

internal sealed class StringTupleComparer : IEqualityComparer<(string, string, string)>
{
    public static StringTupleComparer Instance { get; } = new();

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

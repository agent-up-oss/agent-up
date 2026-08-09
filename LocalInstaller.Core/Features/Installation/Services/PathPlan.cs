namespace AgentUp.Installers.Features.Installation.Services;

public sealed record PathPlan(IReadOnlyList<string> Entries)
{
    public string ToPathString(char separator)
        => string.Join(separator, Entries);
}

public static class PathPlanner
{
    public static PathPlan Add(string currentPath, string managedEntry, char separator)
    {
        var entries = Split(currentPath, separator);
        if (!entries.Any(entry => SamePath(entry, managedEntry)))
            entries.Add(Normalize(managedEntry));

        return new PathPlan(entries);
    }

    public static PathPlan Remove(string currentPath, string managedEntry, char separator)
    {
        var entries = Split(currentPath, separator)
            .Where(entry => !SamePath(entry, managedEntry))
            .ToList();

        return new PathPlan(entries);
    }

    private static List<string> Split(string currentPath, char separator)
        => currentPath
            .Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Normalize)
            .Where(entry => !string.IsNullOrWhiteSpace(entry))
            .ToList();

    private static string Normalize(string path)
        => path.Trim().TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

    private static bool SamePath(string left, string right)
        => string.Equals(Normalize(left), Normalize(right), OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal);
}

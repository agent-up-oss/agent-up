using System.Text.RegularExpressions;

namespace AgentUp.InstallerConfig;

public static partial class RepositoryDotEnv
{
    public static void LoadOptional()
    {
        var path = FindDotEnvFile();
        if (path is null) return;

        Apply(File.ReadAllLines(path), path);
    }

    public static string? FindDotEnvFile()
    {
        var directory = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(directory))
        {
            var candidate = Path.Join(directory, ".env");
            if (File.Exists(candidate)) return candidate;

            var parent = Directory.GetParent(directory)?.FullName;
            if (string.IsNullOrWhiteSpace(parent) || string.Equals(parent, directory, StringComparison.Ordinal))
                break;

            directory = parent;
        }

        return null;
    }

    internal static void Apply(IEnumerable<string> lines, string sourceName)
    {
        foreach (var (key, value) in Parse(lines, sourceName))
        {
            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    internal static Dictionary<string, string> Parse(IEnumerable<string> lines, string sourceName)
    {
        var environment = new Dictionary<string, string>();
        var lineNumber = 0;
        foreach (var rawLine in lines)
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
                continue;

            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line["export ".Length..].TrimStart();

            var equalsIndex = line.IndexOf('=');
            if (equalsIndex <= 0)
                throw new InvalidOperationException($"Environment file '{sourceName}' has an invalid entry on line {lineNumber}.");

            var key = line[..equalsIndex].Trim();
            if (!EnvironmentVariableName().IsMatch(key))
                throw new InvalidOperationException($"Environment file '{sourceName}' has an invalid variable name on line {lineNumber}.");

            var value = line[(equalsIndex + 1)..].Trim();
            if (value.Length >= 2
                && ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            environment[key] = value;
        }

        return environment;
    }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex EnvironmentVariableName();
}

using System.Globalization;
using System.Text.Json;
using AgentUp.Verification.Features.Coverage.Interfaces;
using AgentUp.Verification.Features.Coverage.Models;
using AgentUp.Verification.Shared.Providers;

namespace AgentUp.Verification.Features.Coverage.Providers;

/// <summary>
/// Reads the "coverage" section of agent-up.json. Every malformed input throws.
/// </summary>
public sealed class CoverageConfigurationLoader : ICoverageConfigurationLoader
{
    private const string SectionName = "coverage";

    public CoverageConfiguration Load(string repositoryRoot)
    {
        var path = Path.Join(repositoryRoot, "agent-up.json");
        if (!File.Exists(path))
            return CoverageConfiguration.Empty;

        using var document = ParseOrThrow(path);
        if (!document.RootElement.TryGetProperty(SectionName, out var section))
            return CoverageConfiguration.Empty;

        if (section.ValueKind != JsonValueKind.Object)
            throw Invalid("'coverage' must be an object.");

        var minimum = ReadMinimum(section);
        var reportDirectory = ReadString(section, "reportDirectory") ?? "artifacts/coverage";
        var include = ReadGlobs(section, "include");
        var exclude = ReadGlobs(section, "exclude");

        if (include.Count == 0)
            throw Invalid("'coverage.include' must list at least one glob, or the gate measures nothing.");

        return new CoverageConfiguration(
            minimum,
            PathGlobProvider.Normalize(reportDirectory),
            include,
            exclude);
    }

    private static JsonDocument ParseOrThrow(string path)
    {
        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            throw new CoverageConfigurationException($"'{path}' is not valid JSON: {exception.Message}", exception);
        }
        catch (IOException exception)
        {
            throw new CoverageConfigurationException($"Could not read '{path}'.", exception);
        }
    }

    private static double ReadMinimum(JsonElement section)
    {
        if (!section.TryGetProperty("minimum", out var value))
            throw Invalid("'coverage.minimum' is required.");

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var minimum))
            throw Invalid("'coverage.minimum' must be a number.");

        if (minimum is < 0d or > 100d)
        {
            throw Invalid(
                $"'coverage.minimum' must be between 0 and 100, not {minimum.ToString(CultureInfo.InvariantCulture)}.");
        }

        return minimum;
    }

    private static string? ReadString(JsonElement section, string name)
        => section.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static IReadOnlyList<string> ReadGlobs(JsonElement section, string name)
    {
        if (!section.TryGetProperty(name, out var value))
            return [];

        if (value.ValueKind != JsonValueKind.Array)
            throw Invalid($"'coverage.{name}' must be an array of globs.");

        return
        [
            .. value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => PathGlobProvider.Normalize(item.GetString() ?? string.Empty))
                .Where(item => item.Length > 0)
        ];
    }

    private static CoverageConfigurationException Invalid(string message)
        => new($"Invalid 'coverage' configuration in agent-up.json: {message}");
}

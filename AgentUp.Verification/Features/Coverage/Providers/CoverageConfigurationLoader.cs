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

        var minimum = ReadPercentage(section, "minimum", required: true);
        var reportDirectory = ReadString(section, "reportDirectory") ?? "artifacts/coverage";
        var include = ReadGlobs(section, "include");
        var exclude = ReadGlobs(section, "exclude");
        var sliceMinimum = ReadPercentage(section, "sliceMinimum", required: false);
        var sliceExemptions = ReadSliceExemptions(section);

        if (include.Count == 0)
            throw Invalid("'coverage.include' must list at least one glob, or the gate measures nothing.");

        return new CoverageConfiguration(
            minimum,
            PathGlobProvider.Normalize(reportDirectory),
            include,
            exclude,
            sliceMinimum,
            sliceExemptions);
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

    /// <summary>
    /// Reads a percentage. An optional one that is absent reads as 0, which switches its
    /// floor off rather than failing: a repository without a slice layout has no slices to
    /// hold to one.
    /// </summary>
    private static double ReadPercentage(JsonElement section, string name, bool required)
    {
        if (!section.TryGetProperty(name, out var value))
        {
            if (required)
                throw Invalid($"'coverage.{name}' is required.");

            return 0d;
        }

        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var percentage))
            throw Invalid($"'coverage.{name}' must be a number.");

        if (percentage is < 0d or > 100d)
        {
            throw Invalid(
                $"'coverage.{name}' must be between 0 and 100, not {percentage.ToString(CultureInfo.InvariantCulture)}.");
        }

        return percentage;
    }

    private static IReadOnlyList<string> ReadSliceExemptions(JsonElement section)
    {
        if (!section.TryGetProperty("sliceExemptions", out var value))
            return [];

        if (value.ValueKind != JsonValueKind.Array)
            throw Invalid("'coverage.sliceExemptions' must be an array of slice paths.");

        var entries = value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String
                ? PathGlobProvider.Normalize(item.GetString() ?? string.Empty)
                : throw Invalid("'coverage.sliceExemptions' entries must be strings."))
            .ToArray();

        // Entries are compared to exact slice paths, so anything else - a type folder, a
        // whole project, a glob - can never match. Left in, it reads as an exemption that
        // is doing something while silently exempting nothing.
        var malformed = entries
            .Where(entry => entry.Split('/') is not [_, "Features", _]
                            || entry.AsSpan().ContainsAny('*', '?'))
            .ToArray();

        if (malformed.Length > 0)
        {
            throw Invalid(
                "'coverage.sliceExemptions' entries must be exactly '<Project>/Features/<Slice>': "
                + string.Join(", ", malformed));
        }

        return entries;
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

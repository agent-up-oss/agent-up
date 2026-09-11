using System.Text.Json;
using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Reads the "verification" section of agent-up.json.
/// </summary>
/// <remarks>
/// Every malformed input throws. This is the deliberate opposite of the commit-queue
/// loader it replaces, which swallowed JsonException and IOException and returned an empty
/// configuration — turning a typo in agent-up.json into a silently disabled gate that
/// still reported success.
/// </remarks>
public sealed class VerificationConfigurationLoader : IVerificationConfigurationLoader
{
    private const string SectionName = "verification";

    private static readonly string[] KnownPlatforms = ["linux", "macos", "windows"];

    public VerificationConfiguration Load(string repositoryRoot)
    {
        var path = Path.Join(repositoryRoot, "agent-up.json");
        if (!File.Exists(path))
            return VerificationConfiguration.Empty;

        var text = ReadOrThrow(path);
        using var document = ParseOrThrow(path, text);

        if (!document.RootElement.TryGetProperty(SectionName, out var section))
            return VerificationConfiguration.Empty;

        if (section.ValueKind != JsonValueKind.Object)
            throw Invalid($"'{SectionName}' must be an object.");

        var checks = ReadChecks(section);
        var paths = ReadPaths(section);
        var always = ReadStringArray(section, "always");
        var enforcement = ReadEnforcement(section);

        EnsureReferencesResolve(checks, paths, always);

        return new VerificationConfiguration(enforcement, always, checks, paths);
    }

    private static string ReadOrThrow(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (IOException exception)
        {
            throw new VerificationConfigurationException(
                $"Could not read '{path}'. Verification cannot run without its rules.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new VerificationConfigurationException(
                $"Could not read '{path}'. Verification cannot run without its rules.", exception);
        }
    }

    private static JsonDocument ParseOrThrow(string path, string text)
    {
        try
        {
            return JsonDocument.Parse(text);
        }
        catch (JsonException exception)
        {
            throw new VerificationConfigurationException(
                $"'{path}' is not valid JSON: {exception.Message}", exception);
        }
    }

    private static IReadOnlyDictionary<string, CheckDefinition> ReadChecks(JsonElement section)
    {
        if (!section.TryGetProperty("checks", out var checks))
            throw Invalid($"'{SectionName}' must declare a 'checks' object.");

        if (checks.ValueKind != JsonValueKind.Object)
            throw Invalid("'checks' must be an object keyed by check id.");

        return checks.EnumerateObject()
            .ToDictionary(
                property => property.Name,
                property => ReadCheck(property.Name, property.Value),
                StringComparer.Ordinal);
    }

    private static CheckDefinition ReadCheck(string id, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw Invalid($"Check '{id}' must be an object.");

        var command = ReadRequiredString(element, "command", $"Check '{id}'");
        var workingDirectory = ReadOptionalString(element, "workingDirectory");
        var tier = ReadTier(id, element);
        var platforms = ReadStringArray(element, "platforms");
        var ciOnly = ReadBoolean(element, "ciOnly");
        var inputs = ReadStringArray(element, "inputs")
            .Select(PathGlobProvider.Normalize)
            .ToArray();

        var unknownPlatform = platforms.FirstOrDefault(platform => !KnownPlatforms.Contains(platform, StringComparer.Ordinal));
        if (unknownPlatform is not null)
        {
            throw Invalid(
                $"Check '{id}' declares unknown platform '{unknownPlatform}'. " +
                $"Expected one of: {string.Join(", ", KnownPlatforms)}.");
        }

        return new CheckDefinition(id, command, workingDirectory, tier, platforms, ciOnly, inputs);
    }

    private static IReadOnlyList<VerificationPathRule> ReadPaths(JsonElement section)
    {
        if (!section.TryGetProperty("paths", out var paths))
            throw Invalid($"'{SectionName}' must declare a 'paths' array.");

        if (paths.ValueKind != JsonValueKind.Array)
            throw Invalid("'paths' must be an array of rules.");

        return [.. paths.EnumerateArray().Select(ReadPathRule)];
    }

    private static VerificationPathRule ReadPathRule(JsonElement element, int index)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw Invalid($"Path rule at index {index} must be an object.");

        var match = ReadRequiredString(element, "match", $"Path rule at index {index}");

        if (!element.TryGetProperty("checks", out var checks) || checks.ValueKind != JsonValueKind.Array)
        {
            throw Invalid(
                $"Path rule '{match}' must declare a 'checks' array. " +
                "Use an empty array to state explicitly that these paths require nothing.");
        }

        return new VerificationPathRule(PathGlobProvider.Normalize(match), ReadStringArray(element, "checks"));
    }

    private static CheckTier ReadTier(string id, JsonElement element)
    {
        var raw = ReadOptionalString(element, "tier");
        if (raw is null)
            return CheckTier.Fast;

        return raw switch
        {
            "fast" => CheckTier.Fast,
            "slow" => CheckTier.Slow,
            "platform" => CheckTier.Platform,
            _ => throw Invalid($"Check '{id}' declares unknown tier '{raw}'. Expected fast, slow or platform.")
        };
    }

    private static VerificationEnforcement ReadEnforcement(JsonElement section)
    {
        var raw = ReadOptionalString(section, "enforcement");
        if (raw is null)
            return VerificationEnforcement.Warn;

        return raw switch
        {
            "warn" => VerificationEnforcement.Warn,
            "block" => VerificationEnforcement.Block,
            _ => throw Invalid($"Unknown enforcement '{raw}'. Expected warn or block.")
        };
    }

    private static void EnsureReferencesResolve(
        IReadOnlyDictionary<string, CheckDefinition> checks,
        IReadOnlyList<VerificationPathRule> paths,
        IReadOnlyList<string> always)
    {
        var danglingFromPaths = paths
            .SelectMany(rule => rule.Checks.Select(check => (rule.Match, Check: check)))
            .Where(reference => !checks.ContainsKey(reference.Check))
            .Select(reference => $"path rule '{reference.Match}' references unknown check '{reference.Check}'");

        var danglingFromAlways = always
            .Where(check => !checks.ContainsKey(check))
            .Select(check => $"'always' references unknown check '{check}'");

        var dangling = danglingFromPaths.Concat(danglingFromAlways).ToArray();
        if (dangling.Length > 0)
            throw Invalid(string.Join("; ", dangling));
    }

    private static string ReadRequiredString(JsonElement element, string name, string owner)
    {
        var value = ReadOptionalString(element, name);
        if (string.IsNullOrWhiteSpace(value))
            throw Invalid($"{owner} must declare a non-empty '{name}'.");

        return value;
    }

    private static string? ReadOptionalString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadBoolean(JsonElement element, string name)
        => element.TryGetProperty(name, out var value)
           && value.ValueKind is JsonValueKind.True or JsonValueKind.False
           && value.GetBoolean();

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            return [];

        return
        [
            .. value.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty)
                .Where(item => !string.IsNullOrWhiteSpace(item))
        ];
    }

    private static VerificationConfigurationException Invalid(string message)
        => new($"Invalid '{SectionName}' configuration in agent-up.json: {message}");
}

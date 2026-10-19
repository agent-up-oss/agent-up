using System.Text.RegularExpressions;

namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>Reads and compares dotted toolchain versions. A missing or unreadable version does not meet a floor.</summary>
public static partial class ToolchainVersion
{
    public static string? Read(string text)
    {
        var match = VersionPattern().Match(text);
        return match.Success ? match.Value : null;
    }

    public static bool Meets(string? found, string minimum)
    {
        if (!TryComponents(found, out var actual) || !TryComponents(minimum, out var required))
            return false;

        var length = Math.Max(actual.Count, required.Count);
        for (var index = 0; index < length; index++)
        {
            var left = index < actual.Count ? actual[index] : 0;
            var right = index < required.Count ? required[index] : 0;
            if (left == right)
                continue;

            return left > right;
        }

        return true;
    }

    private static bool TryComponents(string? text, out List<int> components)
    {
        components = [];
        var read = text is null ? null : Read(text);
        if (read is null)
            return false;

        foreach (var part in read.Split('.'))
        {
            if (!int.TryParse(part, out var value))
                return false;

            components.Add(value);
        }

        return components.Count > 0;
    }

    [GeneratedRegex(@"\d+(?:\.\d+){1,3}")]
    private static partial Regex VersionPattern();
}

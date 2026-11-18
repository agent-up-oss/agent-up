namespace AgentUp.Sdk.Common;

/// <summary>One value a user configures for a capability.</summary>
/// <param name="Key">Stable key the value is stored under.</param>
/// <param name="Label">Short field label a client shows.</param>
/// <param name="Kind">One of <see cref="CapabilitySettingKind.Allowed"/>.</param>
/// <param name="Required">Whether the capability's feature stays unavailable until it is set.</param>
/// <param name="Placeholder">Example value a client may show in an empty field.</param>
public sealed record CapabilitySettingSpec(
    string Key,
    string Label,
    string Kind,
    bool Required = false,
    string? Placeholder = null)
{
    /// <summary>The keys of required settings that have no non-blank value.</summary>
    public static IReadOnlyList<string> MissingRequired(
        IReadOnlyList<CapabilitySettingSpec> specs,
        IReadOnlyDictionary<string, string> values)
        => specs
            .Where(spec => spec.Required
                           && (!values.TryGetValue(spec.Key, out var value) || string.IsNullOrWhiteSpace(value)))
            .Select(spec => spec.Key)
            .ToArray();
}

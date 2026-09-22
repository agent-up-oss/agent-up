using System.Text.Json;

namespace AgentUp.Sdk.Runtime;

public static class RuntimeSectionBinder
{
    public static RuntimeBindResult Bind(
        IReadOnlyList<IReadOnlyDictionary<string, JsonElement>> items,
        IReadOnlyList<RuntimeAttributeSpec> extraAttributes)
    {
        var extras = extraAttributes.ToDictionary(spec => spec.Name, StringComparer.OrdinalIgnoreCase);
        var allowed = RuntimeCommonAttributes.Names
            .Concat(extras.Keys)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var messages = new List<string>();
        var bound = new List<IReadOnlyDictionary<string, string>>();

        foreach (var item in items.Select(PromoteNestedRun))
        {
            messages.AddRange(item.Keys
                .Where(key => !allowed.Contains(key))
                .Select(key => $"Unknown runtime attribute '{key}'."));
            messages.AddRange(extraAttributes
                .Where(spec => spec.Required)
                .Where(spec => !item.TryGetValue(spec.Name, out var value) || IsMissing(value))
                .Select(spec => $"Runtime attribute '{spec.Name}' is required."));

            var flattened = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in item)
                flattened[key] = Flatten(value);
            bound.Add(flattened);
        }

        return new RuntimeBindResult(messages.Count == 0, messages, bound);
    }

    public static IReadOnlyDictionary<string, JsonElement> PromoteNestedRun(
        IReadOnlyDictionary<string, JsonElement> item)
    {
        if (!item.TryGetValue("run", out var run) || run.ValueKind != JsonValueKind.Object)
            return item;

        var promoted = new Dictionary<string, JsonElement>(item, StringComparer.OrdinalIgnoreCase);
        if (run.TryGetProperty("project", out var project) && !HasValue(promoted, "project"))
            promoted["project"] = project;
        if (run.TryGetProperty("arguments", out var arguments) && !HasValue(promoted, "arguments"))
            promoted["arguments"] = arguments;
        return promoted;
    }

    private static bool HasValue(IReadOnlyDictionary<string, JsonElement> item, string name)
        => item.TryGetValue(name, out var value) && !IsMissing(value);

    private static bool IsMissing(JsonElement value)
        => value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
           || (value.ValueKind == JsonValueKind.String && string.IsNullOrWhiteSpace(value.GetString()));

    private static string Flatten(JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.Null => "",
            _ => value.GetRawText()
        };
}

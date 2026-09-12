using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentEventFrameProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public JsonElement Payload(object value) => JsonSerializer.SerializeToElement(value, JsonOptions);
    public string Frame(AgentEventDto item) => $"id: {item.Sequence}\nevent: {item.Type}\ndata: {JsonSerializer.Serialize(item, JsonOptions)}\n\n";
    public IReadOnlySet<string> PermissionOptionIds(JsonElement request)
    {
        if (!request.TryGetProperty("options", out var options) || options.ValueKind != JsonValueKind.Array)
            return new HashSet<string>(StringComparer.Ordinal);
        return options.EnumerateArray()
            .Select(option => option.TryGetProperty("optionId", out var id) ? id.GetString() : null)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .ToHashSet(StringComparer.Ordinal);
    }

    public IReadOnlyList<AgentAuthMethodDto> AuthMethods(JsonElement initialized)
    {
        if (!initialized.TryGetProperty("authMethods", out var methods) || methods.ValueKind != JsonValueKind.Array)
            return [];
        return methods.EnumerateArray()
            .Where(method => method.TryGetProperty("id", out _))
            .Where(method => !method.TryGetProperty("type", out var type) || type.GetString() != "terminal")
            .Select(method =>
            new AgentAuthMethodDto(
                method.GetProperty("id").GetString() ?? string.Empty,
                method.TryGetProperty("name", out var name) ? name.GetString() ?? "Authentication" : "Authentication",
                method.TryGetProperty("description", out var description) ? description.GetString() : null))
            .Where(method => !string.IsNullOrWhiteSpace(method.Id)).ToArray();
    }
}

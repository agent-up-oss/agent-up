using System.Text.Json;

namespace AgentUp.Browser.Streaming.Tests.Support;

/// <summary>
/// Builds the input messages a streaming client sends, so a test states only the fields it
/// is about rather than a full JSON literal.
/// </summary>
internal sealed class BrowserInputJson
{
    private readonly Dictionary<string, object?> _fields = new(StringComparer.Ordinal);

    public static BrowserInputJson OfType(string type) => new BrowserInputJson().With("type", type);

    /// <summary>A message with no "type" field at all, which the parser must reject.</summary>
    public static BrowserInputJson Untyped() => new();

    public BrowserInputJson With(string name, object? value)
    {
        _fields[name] = value;
        return this;
    }

    public BrowserInputJson At(double x, double y) => With("x", x).With("y", y);

    public BrowserInputJson Without(string name)
    {
        _fields.Remove(name);
        return this;
    }

    public string Build() => JsonSerializer.Serialize(_fields);
}

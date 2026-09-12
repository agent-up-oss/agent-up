using System.Text.Json;
using AgentUp.Browser.Streaming.Models;
using PuppeteerSharp.Input;

namespace AgentUp.Browser.Streaming;

/// <summary>
/// Decodes a streaming client's input message into a <see cref="BrowserInputCommand"/>.
/// </summary>
/// <remarks>
/// Pure on purpose: this is the half of the input path that can be tested without a
/// browser. Missing coordinates and a missing "type" still throw, exactly as reading the
/// JSON inline did, because the dispatcher relies on catching those to log and drop a
/// malformed message rather than acting on half of it.
/// </remarks>
public sealed class BrowserInputParser
{
    public BrowserInputCommand Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Parse(document.RootElement);
    }

    public BrowserInputCommand Parse(JsonElement root)
    {
        var type = root.GetProperty("type").GetString();
        var kind = KindOf(type);

        return new BrowserInputCommand(
            type,
            kind,
            Coordinate(root, kind, "x"),
            Coordinate(root, kind, "y"),
            Delta(root, kind, "deltaX"),
            Delta(root, kind, "deltaY"),
            Text(root, kind, "key", BrowserInputKind.KeyDown, BrowserInputKind.KeyUp),
            Text(root, kind, "text", BrowserInputKind.Type),
            ButtonOf(root),
            ClickCountOf(root),
            OptionalInt(root, "width"),
            OptionalInt(root, "height"));
    }

    private static BrowserInputKind KindOf(string? type)
        => type switch
        {
            "mousemove" => BrowserInputKind.MouseMove,
            "mousedown" => BrowserInputKind.MouseDown,
            "mouseup" => BrowserInputKind.MouseUp,
            "click" => BrowserInputKind.Click,
            "wheel" => BrowserInputKind.Wheel,
            "keydown" => BrowserInputKind.KeyDown,
            "keyup" => BrowserInputKind.KeyUp,
            "type" => BrowserInputKind.Type,
            "controlmode" => BrowserInputKind.ControlMode,
            _ => BrowserInputKind.Unknown
        };

    /// <summary>
    /// Coordinates are read only for the kinds that use them, so a keystroke message is
    /// not rejected for lacking an x. For the kinds that do use them, a missing property
    /// throws as before.
    /// </summary>
    private static decimal Coordinate(JsonElement root, BrowserInputKind kind, string property)
        => kind is BrowserInputKind.MouseMove or BrowserInputKind.MouseDown
                or BrowserInputKind.MouseUp or BrowserInputKind.Click
            ? (decimal)root.GetProperty(property).GetDouble()
            : 0m;

    private static decimal Delta(JsonElement root, BrowserInputKind kind, string property)
        => kind == BrowserInputKind.Wheel
            ? (decimal)root.GetProperty(property).GetDouble()
            : 0m;

    private static string Text(
        JsonElement root,
        BrowserInputKind kind,
        string property,
        params BrowserInputKind[] kinds)
        => kinds.Contains(kind)
            ? root.GetProperty(property).GetString() ?? string.Empty
            : string.Empty;

    private static MouseButton ButtonOf(JsonElement root)
        => (root.TryGetProperty("button", out var button) ? button.GetString() : null) switch
        {
            "middle" => MouseButton.Middle,
            "right" => MouseButton.Right,
            _ => MouseButton.Left
        };

    private static int ClickCountOf(JsonElement root)
        => root.TryGetProperty("clickCount", out var count) ? count.GetInt32() : 1;

    private static int? OptionalInt(JsonElement root, string property)
        => root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
}

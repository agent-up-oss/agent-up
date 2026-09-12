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
            ? Number(root, property)
            : 0m;

    private static decimal Delta(JsonElement root, BrowserInputKind kind, string property)
        => kind == BrowserInputKind.Wheel
            ? Number(root, property)
            : 0m;

    /// <summary>
    /// Reads a number the command can actually carry.
    /// </summary>
    /// <remarks>
    /// A number outside decimal's range is a malformed message, so it is rejected the way a
    /// missing property is rather than clamped: acting on part of a malformed message is
    /// what the dispatcher's catch exists to prevent. It has to be rejected as a
    /// JsonException, because the two exceptions this replaces - System.Text.Json's
    /// FormatException for a double it cannot represent, and the decimal cast's
    /// OverflowException - are outside the dispatcher's filter, so a client sending
    /// {"type":"click","x":1e308} faulted the dispatch task instead of dropping one frame.
    /// </remarks>
    private static decimal Number(JsonElement root, string property)
    {
        var value = root.GetProperty(property);

        // Strict bounds: a double below (double)decimal.MaxValue is at most the next
        // representable double down, which is far inside decimal's range, so the cast
        // cannot overflow.
        if (!value.TryGetDouble(out var number)
            || double.IsNaN(number)
            || Math.Abs(number) >= (double)decimal.MaxValue)
        {
            throw new JsonException($"'{property}' is not a number this input can carry.");
        }

        return (decimal)number;
    }

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

    /// <summary>
    /// A click count outside int range falls back to a single click rather than throwing:
    /// it refines the gesture instead of aiming it, so one click is a safe reading of a
    /// nonsense value.
    /// </summary>
    private static int ClickCountOf(JsonElement root)
        => root.TryGetProperty("clickCount", out var count) && count.TryGetInt32(out var clicks)
            ? clicks
            : 1;

    /// <summary>
    /// An out-of-range viewport dimension reads as absent, which leaves the viewport
    /// untouched. TryGetInt32 rather than GetInt32: the latter throws FormatException for a
    /// number too large for an int, and the dispatcher does not catch that.
    /// </summary>
    private static int? OptionalInt(JsonElement root, string property)
        => root.TryGetProperty(property, out var value)
           && value.ValueKind == JsonValueKind.Number
           && value.TryGetInt32(out var number)
            ? number
            : null;
}

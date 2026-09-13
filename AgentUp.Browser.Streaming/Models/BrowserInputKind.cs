namespace AgentUp.Browser.Streaming.Models;

/// <summary>
/// The kind of input a streaming client asked for.
/// </summary>
public enum BrowserInputKind
{
    /// <summary>A type this build does not handle. Dispatched as a no-op.</summary>
    Unknown,
    MouseMove,
    MouseDown,
    MouseUp,
    Click,
    Wheel,
    KeyDown,
    KeyUp,
    Type,
    ControlMode
}

namespace AgentUp.Browser.Streaming.Models;

/// <summary>
/// Maps a CSS cursor value to the small set the streaming client renders.
/// </summary>
public static class BrowserCursorKind
{
    public const string Default = "default";

    public static string From(string? cursor)
        => cursor switch
        {
            "pointer" => "pointer",
            "text" or "vertical-text" => "text",
            "grab" or "grabbing" => "grab",
            _ => Default
        };
}

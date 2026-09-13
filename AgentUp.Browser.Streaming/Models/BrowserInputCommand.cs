using PuppeteerSharp.Input;

namespace AgentUp.Browser.Streaming.Models;

/// <summary>
/// One decoded input message from a streaming client.
/// </summary>
/// <remarks>
/// Separating decoding from dispatch is what makes the input path testable: deciding what
/// the client asked for needs no browser, while telling PuppeteerSharp to do it does.
/// </remarks>
/// <param name="Type">
/// The raw "type" field, kept because a present-but-unhandled type still counts as input
/// activity while a missing one does not.
/// </param>
public sealed record BrowserInputCommand(
    string? Type,
    BrowserInputKind Kind,
    decimal X,
    decimal Y,
    decimal DeltaX,
    decimal DeltaY,
    string Key,
    string Text,
    MouseButton Button,
    int ClickCount,
    int? Width,
    int? Height)
{
    public ClickOptions ClickOptions => new() { Button = Button, Count = ClickCount };

    /// <summary>Whether a control-mode message carried an explicit viewport.</summary>
    public bool HasViewport => Width is not null && Height is not null;
}

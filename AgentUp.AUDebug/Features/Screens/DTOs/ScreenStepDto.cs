using AgentUp.AUDebug.Features.Screens.Models;

namespace AgentUp.AUDebug.Features.Screens.DTOs;

/// <summary>One step of a product screen route. See <see cref="ScreenStepKind"/> for the fields each kind reads.</summary>
public sealed record ScreenStepDto(
    ScreenStepKind Kind,
    int X = 0,
    int Y = 0,
    string? Target = null,
    string? Text = null,
    int DelayMs = ScreenStepDto.DefaultDelayMs)
{
    public const int DefaultDelayMs = 900;

    public static ScreenStepDto Click(int x, int y, int delayMs = DefaultDelayMs)
        => new(ScreenStepKind.Click, x, y, DelayMs: delayMs);

    public static ScreenStepDto Type(string text) => new(ScreenStepKind.Type, Text: text);

    public static ScreenStepDto Key(string chord) => new(ScreenStepKind.Key, Text: chord);

    public static ScreenStepDto Navigate(string path, int delayMs = 2500)
        => new(ScreenStepKind.Navigate, Target: path, DelayMs: delayMs);

    public static ScreenStepDto Tap(string label, int delayMs = 1200)
        => new(ScreenStepKind.Tap, Target: label, DelayMs: delayMs);

    public static ScreenStepDto Fill(string placeholder, string value)
        => new(ScreenStepKind.Fill, Target: placeholder, Text: value);

    public static ScreenStepDto Settle(int delayMs) => new(ScreenStepKind.Settle, DelayMs: delayMs);
}

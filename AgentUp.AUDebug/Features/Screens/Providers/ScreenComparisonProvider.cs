using System.Text.RegularExpressions;
using AgentUp.AUDebug.Features.Screens.DTOs;
using AgentUp.AUDebug.Features.Screens.Interfaces;

namespace AgentUp.AUDebug.Features.Screens.Providers;

/// <summary>
/// Holds the screens the design system documents against the ones the real clients showed.
/// </summary>
/// <remarks>
/// Two capture paths existed and nothing joined them: <c>au-debug screenshots persist</c> wrote
/// the design system's own renders and <c>au-debug screens</c> photographed the real clients,
/// while the only pixel comparison in the repository was a design-system render against its own
/// committed PNG. Matching a documented screen to the real one was left to someone looking at
/// both, which is not a signal an agent can iterate against.
/// <para>
/// The comparison is on copy rather than pixels. Fonts, real timestamps and antialiasing differ
/// between a Chromium render and an Avalonia window, so a pixel diff between the two is noise;
/// what a screen says is the part that is supposed to agree, and a missing string names exactly
/// which part drifted.
/// </para>
/// </remarks>
public sealed class ScreenComparisonProvider : IScreenComparison
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public IReadOnlyList<ScreenComparisonDto> Compare(
        IReadOnlyList<ScreenshotSceneCopyDto> documented,
        IReadOnlyList<ScreenCaptureDto> captured)
    {
        var byId = captured.ToDictionary(capture => capture.Id, StringComparer.Ordinal);
        return documented
            .Select(scene => Compare(scene, byId.GetValueOrDefault(scene.Id)))
            .ToArray();
    }

    private static ScreenComparisonDto Compare(ScreenshotSceneCopyDto scene, ScreenCaptureDto? capture)
    {
        if (capture is null)
        {
            return new ScreenComparisonDto(
                scene.Id,
                ScreenComparisonStatus.NotCaptured,
                [],
                "The design system documents this screen but the run captured nothing for it.");
        }

        if (capture.SkippedReason is not null)
            return new ScreenComparisonDto(scene.Id, ScreenComparisonStatus.Skipped, [], capture.SkippedReason);

        if (capture.Text is null)
        {
            return new ScreenComparisonDto(
                scene.Id,
                ScreenComparisonStatus.NotComparable,
                [],
                $"The {capture.Surface} surface reports no text, so only the capture itself was checked.");
        }

        var shown = Normalise(capture.Text);
        var missing = scene.Copy
            .Where(copy => !shown.Contains(Normalise(copy), StringComparison.Ordinal))
            .ToArray();

        return missing.Length == 0
            ? new ScreenComparisonDto(scene.Id, ScreenComparisonStatus.Matched, [], null)
            : new ScreenComparisonDto(scene.Id, ScreenComparisonStatus.Diverged, missing, null);
    }

    /// <summary>
    /// Collapses whitespace so a line break the real client wraps at does not read as different
    /// copy. Case is kept: the design system decides whether a label is a field label or a
    /// title, and that is exactly the kind of drift worth catching.
    /// </summary>
    private static string Normalise(string value) => Whitespace.Replace(value, " ").Trim();
}

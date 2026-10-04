using AgentUp.AUDebug.Features.Screens.DTOs;

namespace AgentUp.AUDebug.Features.Screens.Interfaces;

/// <summary>
/// Drives one client through its routes against the Demo backend.
/// </summary>
/// <remarks>
/// The session is opened once per run: the Demo state a route builds - an agent transcript,
/// the files that transcript added, a commit - is what the next screen is captured in.
/// </remarks>
public interface IProductScreenSurface : IAsyncDisposable
{
    string Surface { get; }

    Task OpenAsync(CancellationToken cancellationToken);

    Task RunAsync(IReadOnlyList<ScreenStepDto> steps, CancellationToken cancellationToken);

    Task CaptureAsync(string outputPath, CancellationToken cancellationToken);

    /// <summary>
    /// The text the screen is showing, or null where the surface has no way to report it.
    /// </summary>
    /// <remarks>
    /// This is what lets a captured screen be compared with the one the design system
    /// documents. Mobile is driven through the DevTools protocol and can be asked; Desktop is
    /// driven with xdotool and photographed with ImageMagick, which carry no text channel, so
    /// it answers null and the comparison reports those screens as not comparable rather than
    /// quietly passing them.
    /// </remarks>
    Task<string?> ReadTextAsync(CancellationToken cancellationToken);
}

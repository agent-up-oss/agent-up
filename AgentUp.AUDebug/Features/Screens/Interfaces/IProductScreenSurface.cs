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
}

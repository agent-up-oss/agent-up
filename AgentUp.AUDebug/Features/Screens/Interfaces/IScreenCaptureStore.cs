using AgentUp.AUDebug.Features.Screens.DTOs;

namespace AgentUp.AUDebug.Features.Screens.Interfaces;

public interface IScreenCaptureStore
{
    string Reset(string surface);

    string CapturePath(string surface, string view);

    string Relative(string path);

    string WriteManifest(ScreenRunManifestDto manifest);

    /// <summary>
    /// Whether two captures came out byte for byte the same.
    /// </summary>
    /// <remarks>
    /// Desktop is driven by window-relative points read off a real session, so a layout change
    /// moves a control out from under one and the click lands on nothing. The run used to carry
    /// on and photograph a screen that looks plausible and is the wrong one. Two identical
    /// captures in a row mean the steps between them did nothing, which is the symptom that
    /// failure actually has.
    /// </remarks>
    bool IsUnchangedFrom(string path, string? previousPath);
}

using AgentUp.AUDebug.Features.Screens.DTOs;

namespace AgentUp.AUDebug.Features.Screens.Interfaces;

public interface IScreenCaptureStore
{
    string Reset(string surface);

    string CapturePath(string surface, string view);

    string Relative(string path);

    string WriteManifest(ScreenRunManifestDto manifest);
}

using AgentUp.AUDebug.Features.Screens.DTOs;

namespace AgentUp.AUDebug.Features.Screens.Interfaces;

/// <summary>Reads back the manifest a screens run wrote.</summary>
public interface IScreenRunStore
{
    ScreenRunManifestDto? Read();
}

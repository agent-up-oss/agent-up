using AgentUp.AUDebug.Features.Screens.DTOs;

namespace AgentUp.AUDebug.Features.Screens.Interfaces;

/// <summary>The screens the design system documents, and the copy each one shows.</summary>
public interface IDocumentedScreens
{
    IReadOnlyList<ScreenshotSceneCopyDto> Read();
}

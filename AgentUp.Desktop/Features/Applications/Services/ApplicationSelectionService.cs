using AgentUp.Desktop.Features.Applications.ViewModels;

namespace AgentUp.Desktop.Features.Applications.Services;

public sealed class ApplicationSelectionService
{
    public IReadOnlyList<ApplicationViewModel> Normalize(IReadOnlyList<ApplicationViewModel> applications)
        => applications;
}

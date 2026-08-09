using AgentUp.Desktop.Features.FirstRun.Services;

namespace AgentUp.Desktop.Features.FirstRun.Interfaces;

public interface IFirstRunTutorialSettingsStore
{
    Task<FirstRunTutorialSettings> LoadAsync();

    Task SaveAsync(FirstRunTutorialSettings settings);
}

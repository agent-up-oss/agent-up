namespace AgentUp.Desktop.Features.FirstRun.Services;

public interface IFirstRunTutorialSettingsStore
{
    Task<FirstRunTutorialSettings> LoadAsync();

    Task SaveAsync(FirstRunTutorialSettings settings);
}

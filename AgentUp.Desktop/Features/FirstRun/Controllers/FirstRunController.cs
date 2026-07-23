using AgentUp.Desktop.Features.FirstRun.ViewModels;

namespace AgentUp.Desktop.Features.FirstRun.Controllers;

public sealed class FirstRunController
{
    private readonly FirstRunTutorialViewModel _tutorial;

    public FirstRunController(FirstRunTutorialViewModel tutorial)
    {
        _tutorial = tutorial;
    }

    public async Task InitializeAsync()
        => await _tutorial.InitializeAsync();
}

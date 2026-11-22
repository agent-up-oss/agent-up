namespace AgentUp.Desktop.Features.Files.ViewModels;

internal interface IFileNodeHost
{
    Task ToggleAsync(FileNodeViewModel node);

    Task OpenFileAsync(FileNodeViewModel node);
}

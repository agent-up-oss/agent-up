namespace AgentUp.Desktop.Features.Git.ViewModels;

internal interface IGitChangeNodeHost
{
    Task OpenFileAsync(GitChangeNodeViewModel node);

    void NodeSelectionChanged(GitChangeNodeViewModel node);
}

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceShellTabItemViewModel(WorkspaceShellTab kind, string label)
{
    public WorkspaceShellTab Kind { get; } = kind;

    public string Label { get; } = label;
}

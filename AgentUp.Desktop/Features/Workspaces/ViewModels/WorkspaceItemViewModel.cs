using AgentUp.Desktop.Features.Applications.Http;
using AgentUp.Desktop.Features.Applications.ViewModels;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceItemViewModel : ReactiveObject
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Branch { get; }
    public string RepositoryPath { get; }
    public string WorktreePath { get; }
    public string State { get; }
    public string Initials { get; }
    public string StateColor { get; }
    public IReadOnlyList<ApplicationViewModel> Applications { get; }

    public WorkspaceItemViewModel(
        string id, string displayName, string branch,
        string repositoryPath, string worktreePath, string state,
        IReadOnlyList<ApplicationDto>? applications = null)
    {
        Id = id;
        DisplayName = displayName;
        Branch = branch;
        RepositoryPath = repositoryPath;
        WorktreePath = worktreePath;
        State = state;
        Initials = BuildInitials(displayName);
        StateColor = ResolveStateColor(state);
        Applications = (applications ?? [])
            .Select(a => new ApplicationViewModel(a.Name, a.Command, a.PortVariable, a.State))
            .ToList();
    }

    private static string ResolveStateColor(string state) => state switch
    {
        "Running" => "#4cbe78",
        "Failed" => "#b85a5a",
        _ => "#5a5a72"
    };

    private static string BuildInitials(string name)
    {
        var parts = name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }
}

using System.Collections.ObjectModel;
using AgentUp.Desktop.Features.Applications.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceItemViewModel : ReactiveObject
{
    private string _state;
    private string _stateColor;

    public string Id { get; }
    public string DisplayName { get; }
    public string Branch { get; }
    public string RepositoryPath { get; }
    public string RepositoryName { get; }
    public string RepositoryToolTip { get; }
    public string WorktreePath { get; }
    public string Initials { get; }

    public string State
    {
        get => _state;
        private set => this.RaiseAndSetIfChanged(ref _state, value);
    }

    public string StateColor
    {
        get => _stateColor;
        private set => this.RaiseAndSetIfChanged(ref _stateColor, value);
    }

    public ObservableCollection<WorkspaceApplicationViewModel> Applications { get; } = [];

    public WorkspaceItemViewModel(
        string id, string displayName, string branch,
        string repositoryPath, string worktreePath, string state,
        IReadOnlyList<ApplicationDto>? applications = null)
    {
        Id = id;
        DisplayName = displayName;
        Branch = branch;
        RepositoryPath = repositoryPath;
        RepositoryName = LastPathSegment(repositoryPath);
        if (string.IsNullOrWhiteSpace(RepositoryName))
            RepositoryName = displayName;
        RepositoryToolTip = repositoryPath;
        WorktreePath = worktreePath;
        _state = state;
        Initials = BuildInitials(displayName);
        _stateColor = ResolveStateColor(state);
        foreach (var app in applications ?? [])
            Applications.Add(CreateApplication(app));
    }

    // Updates workspace and application state in-place without triggering the SelectedWorkspace
    // change notification, so existing browser sessions and navigation state are undisturbed.
    public void UpdateFrom(string newState, IReadOnlyList<ApplicationDto> applications)
    {
        State = newState;
        StateColor = ResolveStateColor(newState);

        var existingByName = Applications.ToDictionary(a => a.Name);
        var incomingByName = applications.ToDictionary(a => a.Name);

        foreach (var name in existingByName.Keys.Except(incomingByName.Keys).ToList())
            Applications.Remove(existingByName[name]);

        foreach (var app in applications)
        {
            if (existingByName.TryGetValue(app.Name, out var existing))
                existing.UpdateFrom(app.Command, app.State, app.AllocatedPorts);
            else
                Applications.Add(CreateApplication(app));
        }
    }

    public void ApplyStateChange(string newState, IReadOnlyList<(string Name, string State)> appChanges)
    {
        State = newState;
        StateColor = ResolveStateColor(newState);

        var changesByName = appChanges.ToDictionary(a => a.Name, a => a.State);
        foreach (var app in Applications.Where(app => changesByName.ContainsKey(app.Name)))
            app.UpdateState(changesByName[app.Name]);

        foreach (var app in appChanges.Where(app => !Applications.Any(existing => existing.Name == app.Name)))
            Applications.Add(new WorkspaceApplicationViewModel(app.Name, string.Empty, app.State));
    }

    private static WorkspaceApplicationViewModel CreateApplication(ApplicationDto app) =>
        new(app.Name, app.Command, app.State, app.AllocatedPorts);

    private static string ResolveStateColor(string state) => state switch
    {
        "Running" => "#00d66b",
        "Failed" => "#b85a5a",
        _ => "#5a5a72"
    };

    private static string LastPathSegment(string path)
        => path.TrimEnd('/', '\\')
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault() ?? string.Empty;

    private static string BuildInitials(string name)
    {
        var parts = name.Split([' ', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2
            ? $"{parts[0][0]}{parts[1][0]}".ToUpperInvariant()
            : name.Length >= 2 ? name[..2].ToUpperInvariant() : name.ToUpperInvariant();
    }
}

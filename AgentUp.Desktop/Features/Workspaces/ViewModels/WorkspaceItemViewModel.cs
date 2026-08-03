using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Applications.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceItemViewModel : ReactiveObject
{
    private string _state;
    private string _stateColor;
    private string _controlAuthority = "ai";
    private int _viewportWidth;
    private int _viewportHeight;
    private string _selectedAiPresetId = "desktop";

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

    public string ControlAuthority
    {
        get => _controlAuthority;
        private set => this.RaiseAndSetIfChanged(ref _controlAuthority, value);
    }

    public string ControlLabel => _controlAuthority == "human"
        ? "Human"
        : _viewportWidth > 0 ? $"AI · {_viewportWidth}×{_viewportHeight}" : "AI";

    public ReactiveCommand<Unit, Unit> ToggleControlCommand { get; }

    public ObservableCollection<WorkspaceApplicationViewModel> Applications { get; } = [];
    public event EventHandler? ApplicationsChanged;

    public WorkspaceItemViewModel(
        string id, string displayName, string branch,
        string repositoryPath, string worktreePath, string state,
        IReadOnlyList<ApplicationDto>? applications = null,
        Func<string, string?, Task>? toggleControlMode = null)
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

        ToggleControlCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            if (toggleControlMode is null) return;
            var next = _controlAuthority == "human" ? "ai" : "human";
            await toggleControlMode(next, next == "ai" ? _selectedAiPresetId : null);
        });
    }

    public void ApplyControlMode(string authority, int width, int height)
    {
        ControlAuthority = authority;
        _viewportWidth = width;
        _viewportHeight = height;
        if (authority == "ai")
            _selectedAiPresetId = BrowserViewportPresetId(width, height);
        this.RaisePropertyChanged(nameof(ControlLabel));
    }

    private static string BrowserViewportPresetId(int width, int height)
        => (width, height) switch
        {
            (375, 667) => "mobile",
            (768, 1024) => "tablet",
            (1280, 720) => "desktop",
            (1440, 900) => "wide",
            (1920, 1080) => "full-hd",
            _ => "desktop"
        };

    // Updates workspace and application state in-place without triggering the SelectedWorkspace
    // change notification, so existing browser sessions and navigation state are undisturbed.
    public void UpdateFrom(string newState, IReadOnlyList<ApplicationDto> applications)
    {
        State = newState;
        StateColor = ResolveStateColor(newState);
        var applicationsChanged = false;

        var existingByName = Applications.ToDictionary(a => a.Name);
        var incomingByName = applications.ToDictionary(a => a.Name);

        foreach (var name in existingByName.Keys.Except(incomingByName.Keys).ToList())
        {
            applicationsChanged = true;
            Applications.Remove(existingByName[name]);
        }

        foreach (var app in applications)
        {
            if (existingByName.TryGetValue(app.Name, out var existing))
            {
                applicationsChanged |= existing.UpdateFrom(app.Command, app.State, app.AllocatedPorts);
            }
            else
            {
                Applications.Add(CreateApplication(app));
                applicationsChanged = true;
            }
        }

        if (applicationsChanged)
            ApplicationsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyStateChange(string newState, IReadOnlyList<(string Name, string State)> appChanges)
    {
        State = newState;
        StateColor = ResolveStateColor(newState);

        var changesByName = appChanges.ToDictionary(a => a.Name, a => a.State);
        var applicationsChanged = false;
        foreach (var app in Applications.Where(app => changesByName.ContainsKey(app.Name)))
            applicationsChanged |= app.UpdateState(changesByName[app.Name]);

        foreach (var app in appChanges.Where(app => !Applications.Any(existing => existing.Name == app.Name)))
        {
            Applications.Add(new WorkspaceApplicationViewModel(app.Name, string.Empty, app.State));
            applicationsChanged = true;
        }

        if (applicationsChanged)
            ApplicationsChanged?.Invoke(this, EventArgs.Empty);
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

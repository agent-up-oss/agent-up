using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Workspaces.ViewModels;

public sealed class WorkspaceItemViewModel : ReactiveObject
{
    private IWorkspaceItemHost? _host;
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
        private set
        {
            if (_state == value)
                return;

            this.RaiseAndSetIfChanged(ref _state, value);
            RaiseLifecyclePropertiesChanged();
        }
    }

    public string StateColor
    {
        get => _stateColor;
        private set => this.RaiseAndSetIfChanged(ref _stateColor, value);
    }

    public bool ShowStartButton => State is "Stopped" or "Failed" or "Stopping";
    public bool ShowStopButton => State is "Running" or "Starting";
    public bool IsLifecycleBusy => State is "Starting" or "Stopping";

    public ObservableCollection<WorkspaceApplicationViewModel> Applications { get; } = [];
    public event EventHandler? ApplicationsChanged;

    public ReactiveCommand<Unit, Unit> StartCommand { get; }
    public ReactiveCommand<Unit, Unit> StopCommand { get; }
    public ReactiveCommand<Unit, Unit> RequestDeleteCommand { get; }

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
        _stateColor = AppHealthLedRules.StateColor(state);
        foreach (var app in applications ?? [])
            Applications.Add(CreateApplication(app));

        var canStart = this.WhenAnyValue(x => x.ShowStartButton, x => x.IsLifecycleBusy, (show, busy) => show && !busy);
        var canStop = this.WhenAnyValue(x => x.ShowStopButton, x => x.IsLifecycleBusy, (show, busy) => show && !busy);
        StartCommand = ReactiveCommand.CreateFromTask(StartAsync, canStart);
        StopCommand = ReactiveCommand.CreateFromTask(StopAsync, canStop);
        RequestDeleteCommand = ReactiveCommand.Create(RequestDelete);
    }

    internal void SetHost(IWorkspaceItemHost host) => _host = host;

    // Updates workspace and application state in-place without triggering the SelectedWorkspace
    // change notification, so existing browser sessions and navigation state are undisturbed.
    public void UpdateFrom(string newState, IReadOnlyList<ApplicationDto> applications)
    {
        State = newState;
        StateColor = AppHealthLedRules.StateColor(newState);
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
                applicationsChanged |= existing.UpdateFrom(app.Command, app.State, app.AllocatedPorts, app.Database, app.Kind == "Desktop");
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

    internal void ApplyStateChange(
        string newState,
        IReadOnlyList<AppStateChangeDto> appChanges,
        string? healthState = null)
    {
        State = newState;
        StateColor = healthState is not null
            ? AppHealthLedRules.StateColor(healthState)
            : AppHealthLedRules.StateColor(newState);

        var changesByName = appChanges.ToDictionary(a => a.Name);
        var applicationsChanged = false;
        foreach (var app in Applications.Where(app => changesByName.ContainsKey(app.Name)))
        {
            var change = changesByName[app.Name];
            applicationsChanged |= app.UpdateState(change.State, change.PortHealth);
        }

        foreach (var change in appChanges.Where(a => !Applications.Any(existing => existing.Name == a.Name)))
        {
            Applications.Add(new WorkspaceApplicationViewModel(change.Name, string.Empty, change.State, portHealth: change.PortHealth));
            applicationsChanged = true;
        }

        if (applicationsChanged)
            ApplicationsChanged?.Invoke(this, EventArgs.Empty);
    }

    private Task StartAsync()
        => _host?.StartWorkspaceAsync(Id) ?? Task.CompletedTask;

    private Task StopAsync()
        => _host?.StopWorkspaceAsync(Id) ?? Task.CompletedTask;

    private void RequestDelete()
        => _host?.RequestDeleteWorkspace(Id, DisplayName);

    private void RaiseLifecyclePropertiesChanged()
    {
        this.RaisePropertyChanged(nameof(ShowStartButton));
        this.RaisePropertyChanged(nameof(ShowStopButton));
        this.RaisePropertyChanged(nameof(IsLifecycleBusy));
    }

    private static WorkspaceApplicationViewModel CreateApplication(ApplicationDto app) =>
        new(app.Name, app.Command, app.State, app.Database, app.AllocatedPorts, isDesktop: app.Kind == "Desktop");

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

using System.Reactive;
using AgentUp.Desktop.Features.FirstRun.Services;
using ReactiveUI;

namespace AgentUp.Desktop.Features.FirstRun.ViewModels;

public sealed class FirstRunTutorialViewModel : ReactiveObject
{
    public const string AgentUpJsonSnippet = """
        {
          "name": "Agent-Up JavaScript Sample",
          "applications": [
            {
              "name": "React SPA",
              "command": "npm install && npm run dev",
              "path": "web",
              "ports": [
                { "variable": "WEB_PORT", "defaultPort": 5173, "protocol": "http" }
              ]
            },
            {
              "name": "Express API",
              "command": "npm install && npm run dev",
              "path": "api",
              "ports": [
                { "variable": "API_PORT", "defaultPort": 3001, "protocol": "http" }
              ]
            },
            {
              "name": "Postgres",
              "command": "docker compose up database -d",
              "ports": [
                { "variable": "POSTGRES_PORT", "defaultPort": 5432, "protocol": "tcp" }
              ]
            }
          ]
        }
        """;

    private readonly IFirstRunTutorialSettingsStore _settingsStore;
    private readonly IFirstRunTutorialChecks _checks;
    private bool _isVisible;
    private int _currentStep = 1;
    private bool _dockerCheckPassed;
    private bool _environmentSelected;
    private bool _nodeCheckPassed;
    private bool _projectFilesCheckPassed;
    private bool _agentUpJsonCheckPassed;
    private bool _workspaceCheckPassed;
    private bool _duplicateCheckPassed;
    private string? _statusMessage;
    private string? _projectDirectory;

    public FirstRunTutorialViewModel(
        IFirstRunTutorialSettingsStore settingsStore,
        IFirstRunTutorialChecks checks)
    {
        _settingsStore = settingsStore;
        _checks = checks;

        CheckDockerCommand = ReactiveCommand.CreateFromTask(CheckDockerAsync);
        SelectJavaScriptCommand = ReactiveCommand.CreateFromTask(SelectJavaScriptAsync);
        CheckNodeCommand = ReactiveCommand.CreateFromTask(CheckNodeAsync);
        CreateSampleProjectCommand = ReactiveCommand.CreateFromTask(CreateSampleProjectAsync);
        CheckProjectFilesCommand = ReactiveCommand.CreateFromTask(CheckProjectFilesAsync);
        CreateAgentUpJsonCommand = ReactiveCommand.CreateFromTask(CreateAgentUpJsonAsync);
        CheckAgentUpJsonCommand = ReactiveCommand.CreateFromTask(CheckAgentUpJsonAsync);
        StartWorkspaceCommand = ReactiveCommand.CreateFromTask(StartWorkspaceAsync);
        CheckWorkspaceCommand = ReactiveCommand.CreateFromTask(CheckWorkspaceAsync);
        CheckDuplicateCommand = ReactiveCommand.CreateFromTask(CheckDuplicateAsync);
        ContinueCommand = ReactiveCommand.CreateFromTask(ContinueAsync);
        SkipCommand = ReactiveCommand.CreateFromTask(SkipAsync);
    }

    public bool IsVisible
    {
        get => _isVisible;
        private set => this.RaiseAndSetIfChanged(ref _isVisible, value);
    }

    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            this.RaiseAndSetIfChanged(ref _currentStep, value);
            RaiseStepPropertiesChanged();
        }
    }

    public bool ShowDockerStep => CurrentStep == 1;

    public bool ShowEnvironmentStep => CurrentStep == 2;

    public bool ShowNodeStep => CurrentStep == 3;

    public bool ShowSampleStep => CurrentStep == 4;

    public bool ShowAgentUpJsonStep => CurrentStep == 5;

    public bool ShowStartStep => CurrentStep == 6;

    public bool ShowDuplicateStep => CurrentStep == 7;

    public bool DockerCheckPassed
    {
        get => _dockerCheckPassed;
        private set
        {
            this.RaiseAndSetIfChanged(ref _dockerCheckPassed, value);
            this.RaisePropertyChanged(nameof(CanContinue));
        }
    }

    public bool EnvironmentSelected
    {
        get => _environmentSelected;
        private set
        {
            this.RaiseAndSetIfChanged(ref _environmentSelected, value);
            this.RaisePropertyChanged(nameof(CanContinue));
        }
    }

    public bool NodeCheckPassed
    {
        get => _nodeCheckPassed;
        private set
        {
            this.RaiseAndSetIfChanged(ref _nodeCheckPassed, value);
            this.RaisePropertyChanged(nameof(CanContinue));
        }
    }

    public bool ProjectFilesCheckPassed
    {
        get => _projectFilesCheckPassed;
        private set
        {
            this.RaiseAndSetIfChanged(ref _projectFilesCheckPassed, value);
            this.RaisePropertyChanged(nameof(CanContinue));
        }
    }

    public bool AgentUpJsonCheckPassed
    {
        get => _agentUpJsonCheckPassed;
        private set
        {
            this.RaiseAndSetIfChanged(ref _agentUpJsonCheckPassed, value);
            this.RaisePropertyChanged(nameof(CanContinue));
        }
    }

    public bool WorkspaceCheckPassed
    {
        get => _workspaceCheckPassed;
        private set
        {
            this.RaiseAndSetIfChanged(ref _workspaceCheckPassed, value);
            this.RaisePropertyChanged(nameof(CanContinue));
        }
    }

    public bool DuplicateCheckPassed
    {
        get => _duplicateCheckPassed;
        private set
        {
            this.RaiseAndSetIfChanged(ref _duplicateCheckPassed, value);
            this.RaisePropertyChanged(nameof(CanContinue));
        }
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        private set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
    }

    public string? ProjectDirectory
    {
        get => _projectDirectory;
        set => this.RaiseAndSetIfChanged(ref _projectDirectory, value);
    }

    public string PrimaryButtonText => CurrentStep == 7 ? "Enter Agent-Up" : "Continue";

    public bool CanContinue => CurrentStep switch
    {
        1 => DockerCheckPassed,
        2 => EnvironmentSelected,
        3 => NodeCheckPassed,
        4 => ProjectFilesCheckPassed,
        5 => AgentUpJsonCheckPassed,
        6 => WorkspaceCheckPassed,
        7 => DuplicateCheckPassed,
        _ => false
    };

    public string Snippet => AgentUpJsonSnippet;

    public ReactiveCommand<Unit, Unit> CheckDockerCommand { get; }

    public ReactiveCommand<Unit, Unit> SelectJavaScriptCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckNodeCommand { get; }

    public ReactiveCommand<Unit, Unit> CreateSampleProjectCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckProjectFilesCommand { get; }

    public ReactiveCommand<Unit, Unit> CreateAgentUpJsonCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckAgentUpJsonCommand { get; }

    public ReactiveCommand<Unit, Unit> StartWorkspaceCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckWorkspaceCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckDuplicateCommand { get; }

    public ReactiveCommand<Unit, Unit> ContinueCommand { get; }

    public ReactiveCommand<Unit, Unit> SkipCommand { get; }

    public async Task InitializeAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        DockerCheckPassed = settings.CompletedStep >= 1;
        EnvironmentSelected = settings.CompletedStep >= 2;
        NodeCheckPassed = settings.CompletedStep >= 3;
        ProjectFilesCheckPassed = settings.CompletedStep >= 4;
        AgentUpJsonCheckPassed = settings.CompletedStep >= 5;
        WorkspaceCheckPassed = settings.CompletedStep >= 6;
        DuplicateCheckPassed = settings.CompletedStep >= 7;
        CurrentStep = Math.Clamp(settings.CompletedStep + 1, 1, 7);
        IsVisible = !settings.TutorialCompleted && !settings.TutorialSkipped;
    }

    private async Task CheckDockerAsync()
    {
        StatusMessage = "Checking Docker...";
        var result = await _checks.CheckDockerAsync();
        DockerCheckPassed = result.IsSuccess;
        StatusMessage = result.Message;
        if (result.IsSuccess)
            await SaveProgressAsync(1);
    }

    private async Task SelectJavaScriptAsync()
    {
        EnvironmentSelected = true;
        StatusMessage = "Node + JavaScript selected.";
        await SaveProgressAsync(2);
    }

    private async Task CheckNodeAsync()
    {
        StatusMessage = "Checking Node and npm...";
        var result = await _checks.CheckNodeAsync();
        NodeCheckPassed = result.IsSuccess;
        StatusMessage = result.Message;
        if (result.IsSuccess)
            await SaveProgressAsync(3);
    }

    private async Task CreateSampleProjectAsync()
    {
        StatusMessage = "Creating the JavaScript sample project...";
        var result = await _checks.CreateJavaScriptSampleAsync(ProjectDirectory ?? string.Empty);
        ProjectFilesCheckPassed = result.IsSuccess;
        StatusMessage = result.Message;
        if (result.IsSuccess)
            await SaveProgressAsync(4);
    }

    private async Task CheckProjectFilesAsync()
    {
        StatusMessage = "Checking project files...";
        var result = await _checks.CheckJavaScriptProjectFilesAsync(ProjectDirectory ?? string.Empty);
        ProjectFilesCheckPassed = result.IsSuccess;
        StatusMessage = result.Message;
        if (result.IsSuccess)
            await SaveProgressAsync(4);
    }

    private async Task CreateAgentUpJsonAsync()
    {
        StatusMessage = "Creating agent-up.json...";
        var result = await _checks.CreateAgentUpJsonAsync(ProjectDirectory ?? string.Empty);
        AgentUpJsonCheckPassed = result.IsSuccess;
        StatusMessage = result.Message;
        if (result.IsSuccess)
            await SaveProgressAsync(5);
    }

    private async Task CheckAgentUpJsonAsync()
    {
        StatusMessage = "Checking agent-up.json...";
        var result = await _checks.CheckAgentUpJsonAsync(ProjectDirectory ?? string.Empty);
        AgentUpJsonCheckPassed = result.IsSuccess;
        StatusMessage = result.Message;
        if (result.IsSuccess)
            await SaveProgressAsync(5);
    }

    private async Task StartWorkspaceAsync()
    {
        StatusMessage = "Running agent-up start...";
        var result = await _checks.StartJavaScriptWorkspaceAsync(ProjectDirectory ?? string.Empty);
        StatusMessage = result.Message;
    }

    private async Task CheckWorkspaceAsync()
    {
        StatusMessage = "Checking the sample workspace on the Server...";
        var result = await _checks.CheckJavaScriptWorkspaceAsync(ProjectDirectory ?? string.Empty);
        WorkspaceCheckPassed = result.IsSuccess;
        StatusMessage = result.Message;
        if (result.IsSuccess)
            await SaveProgressAsync(6);
    }

    private async Task CheckDuplicateAsync()
    {
        StatusMessage = "Checking duplicated workspaces and port allocation...";
        var result = await _checks.CheckDuplicatedJavaScriptWorkspacesAsync(ProjectDirectory ?? string.Empty);
        DuplicateCheckPassed = result.IsSuccess;
        StatusMessage = result.Message;
        if (result.IsSuccess)
            await SaveProgressAsync(7);
    }

    private async Task ContinueAsync()
    {
        if (!CanContinue) return;

        if (CurrentStep < 7)
        {
            CurrentStep++;
            StatusMessage = null;
            return;
        }

        await _settingsStore.SaveAsync(new FirstRunTutorialSettings(true, false, 7));
        IsVisible = false;
    }

    private async Task SkipAsync()
    {
        await _settingsStore.SaveAsync(new FirstRunTutorialSettings(false, true, Math.Max(0, Math.Min(CurrentStep - 1, 7))));
        IsVisible = false;
    }

    private async Task SaveProgressAsync(int completedStep)
        => await _settingsStore.SaveAsync(new FirstRunTutorialSettings(false, false, completedStep));

    private void RaiseStepPropertiesChanged()
    {
        this.RaisePropertyChanged(nameof(ShowDockerStep));
        this.RaisePropertyChanged(nameof(ShowEnvironmentStep));
        this.RaisePropertyChanged(nameof(ShowNodeStep));
        this.RaisePropertyChanged(nameof(ShowSampleStep));
        this.RaisePropertyChanged(nameof(ShowAgentUpJsonStep));
        this.RaisePropertyChanged(nameof(ShowStartStep));
        this.RaisePropertyChanged(nameof(ShowDuplicateStep));
        this.RaisePropertyChanged(nameof(PrimaryButtonText));
        this.RaisePropertyChanged(nameof(CanContinue));
    }
}

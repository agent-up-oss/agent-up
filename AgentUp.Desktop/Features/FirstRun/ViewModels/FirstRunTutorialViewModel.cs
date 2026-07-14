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
    private bool _projectFilesCreated;
    private bool _projectFilesCheckPassed;
    private bool _agentUpJsonActionTaken;
    private bool _agentUpJsonCheckPassed;
    private bool _startCommandSucceeded;
    private bool _workspaceCheckPassed;
    private bool _duplicateActionTaken;
    private bool _duplicateCheckPassed;
    private string? _statusMessage;
    private string? _projectDirectory;
    private string? _directoryTree;
    private string? _commandOutput;

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
        MarkAgentUpJsonCreatedCommand = ReactiveCommand.Create(MarkAgentUpJsonCreated);
        CreateAgentUpJsonCommand = ReactiveCommand.CreateFromTask(CreateAgentUpJsonAsync);
        CheckAgentUpJsonCommand = ReactiveCommand.CreateFromTask(CheckAgentUpJsonAsync);
        StartWorkspaceCommand = ReactiveCommand.CreateFromTask(StartWorkspaceAsync);
        CheckWorkspaceCommand = ReactiveCommand.CreateFromTask(CheckWorkspaceAsync);
        MarkDuplicateStartedCommand = ReactiveCommand.CreateFromTask(CreateDuplicateAsync);
        CheckDuplicateCommand = ReactiveCommand.CreateFromTask(CheckDuplicateAsync);
        ContinueCommand = ReactiveCommand.CreateFromTask(ContinueAsync);
        BackCommand = ReactiveCommand.Create(GoBack);
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

    public bool CanGoBack => CurrentStep > 1;

    public string TutorialRoot => ProjectDirectory is { Length: > 0 } path
        ? Directory.GetParent(path)?.FullName ?? ""
        : "";

    public bool ShowProjectFileCheckSection => ProjectFilesCreated;

    public bool ShowDirectoryTree => !string.IsNullOrWhiteSpace(DirectoryTree);

    public bool ShowAgentUpJsonCheckSection => AgentUpJsonActionTaken;

    public bool ShowWorkspaceCheckSection => StartCommandSucceeded && !string.IsNullOrWhiteSpace(CommandOutput);

    public bool ShowCommandOutput => !string.IsNullOrWhiteSpace(CommandOutput);

    public bool ShowDuplicateCheckSection => DuplicateActionTaken;

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

    public bool ProjectFilesCreated
    {
        get => _projectFilesCreated;
        private set
        {
            this.RaiseAndSetIfChanged(ref _projectFilesCreated, value);
            this.RaisePropertyChanged(nameof(ShowProjectFileCheckSection));
        }
    }

    public bool AgentUpJsonActionTaken
    {
        get => _agentUpJsonActionTaken;
        private set
        {
            this.RaiseAndSetIfChanged(ref _agentUpJsonActionTaken, value);
            this.RaisePropertyChanged(nameof(ShowAgentUpJsonCheckSection));
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

    public bool DuplicateActionTaken
    {
        get => _duplicateActionTaken;
        private set
        {
            this.RaiseAndSetIfChanged(ref _duplicateActionTaken, value);
            this.RaisePropertyChanged(nameof(ShowDuplicateCheckSection));
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
        set
        {
            this.RaiseAndSetIfChanged(ref _projectDirectory, value);
            this.RaisePropertyChanged(nameof(TutorialRoot));
        }
    }

    public string? DirectoryTree
    {
        get => _directoryTree;
        private set
        {
            this.RaiseAndSetIfChanged(ref _directoryTree, value);
            this.RaisePropertyChanged(nameof(ShowDirectoryTree));
        }
    }

    public string? CommandOutput
    {
        get => _commandOutput;
        private set
        {
            this.RaiseAndSetIfChanged(ref _commandOutput, value);
            this.RaisePropertyChanged(nameof(ShowCommandOutput));
            this.RaisePropertyChanged(nameof(ShowWorkspaceCheckSection));
        }
    }

    public bool StartCommandSucceeded
    {
        get => _startCommandSucceeded;
        private set
        {
            this.RaiseAndSetIfChanged(ref _startCommandSucceeded, value);
            this.RaisePropertyChanged(nameof(ShowWorkspaceCheckSection));
        }
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

    public ReactiveCommand<Unit, Unit> MarkAgentUpJsonCreatedCommand { get; }

    public ReactiveCommand<Unit, Unit> CreateAgentUpJsonCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckAgentUpJsonCommand { get; }

    public ReactiveCommand<Unit, Unit> StartWorkspaceCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckWorkspaceCommand { get; }

    public ReactiveCommand<Unit, Unit> MarkDuplicateStartedCommand { get; }

    public ReactiveCommand<Unit, Unit> CheckDuplicateCommand { get; }

    public ReactiveCommand<Unit, Unit> ContinueCommand { get; }

    public ReactiveCommand<Unit, Unit> BackCommand { get; }

    public ReactiveCommand<Unit, Unit> SkipCommand { get; }

    public async Task InitializeAsync()
    {
        var settings = await _settingsStore.LoadAsync();
        DockerCheckPassed = settings.CompletedStep >= 1;
        EnvironmentSelected = settings.CompletedStep >= 2;
        NodeCheckPassed = settings.CompletedStep >= 3;
        ProjectFilesCreated = settings.CompletedStep >= 4;
        ProjectFilesCheckPassed = settings.CompletedStep >= 4;
        AgentUpJsonActionTaken = settings.CompletedStep >= 5;
        AgentUpJsonCheckPassed = settings.CompletedStep >= 5;
        StartCommandSucceeded = settings.CompletedStep >= 6;
        WorkspaceCheckPassed = settings.CompletedStep >= 6;
        DuplicateActionTaken = settings.CompletedStep >= 7;
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
        var result = await _checks.CreateJavaScriptSampleAsync(ProjectDirectory);
        ProjectFilesCreated = result.IsSuccess;
        ProjectDirectory = result.ProjectDirectory ?? ProjectDirectory;
        DirectoryTree = result.IsSuccess ? BuildDirectoryTree(ProjectDirectory ?? string.Empty) : null;
        StatusMessage = result.Message;
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
        AgentUpJsonActionTaken = result.IsSuccess;
        StatusMessage = result.Message;
    }

    private void MarkAgentUpJsonCreated()
    {
        AgentUpJsonActionTaken = true;
        StatusMessage = "Ready to check agent-up.json.";
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
        StartCommandSucceeded = result.IsSuccess;
        CommandOutput = result.Message;
        StatusMessage = result.IsSuccess
            ? "agent-up start succeeded. Review the output, then check the workspace."
            : "agent-up start failed. Review the output below.";
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

    private async Task CreateDuplicateAsync()
    {
        StatusMessage = "Creating and starting example-agent2...";
        var result = await _checks.CreateDuplicatedJavaScriptSampleAsync(ProjectDirectory ?? string.Empty);
        DuplicateActionTaken = result.IsSuccess;
        CommandOutput = result.Message;
        StatusMessage = result.IsSuccess
            ? "example-agent2 was created and started. Review the output, then check duplicate workspaces."
            : "Creating or starting example-agent2 failed. Review the output below.";
    }

    private async Task ContinueAsync()
    {
        if (!CanContinue) return;

        if (CurrentStep < 7)
        {
            CurrentStep++;
            StatusMessage = null;
            CommandOutput = null;
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

    private void GoBack()
    {
        if (CurrentStep <= 1) return;

        CurrentStep--;
        InvalidateFromStep(CurrentStep);
        StatusMessage = null;
        CommandOutput = null;
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
        this.RaisePropertyChanged(nameof(CanGoBack));
        this.RaisePropertyChanged(nameof(TutorialRoot));
        this.RaisePropertyChanged(nameof(ShowProjectFileCheckSection));
        this.RaisePropertyChanged(nameof(ShowDirectoryTree));
        this.RaisePropertyChanged(nameof(ShowAgentUpJsonCheckSection));
        this.RaisePropertyChanged(nameof(ShowWorkspaceCheckSection));
        this.RaisePropertyChanged(nameof(ShowCommandOutput));
        this.RaisePropertyChanged(nameof(ShowDuplicateCheckSection));
        this.RaisePropertyChanged(nameof(PrimaryButtonText));
        this.RaisePropertyChanged(nameof(CanContinue));
    }

    private void InvalidateFromStep(int step)
    {
        if (step <= 1)
        {
            DockerCheckPassed = false;
            EnvironmentSelected = false;
            NodeCheckPassed = false;
            ProjectFilesCreated = false;
            ProjectFilesCheckPassed = false;
            AgentUpJsonActionTaken = false;
            AgentUpJsonCheckPassed = false;
            StartCommandSucceeded = false;
            WorkspaceCheckPassed = false;
            DuplicateActionTaken = false;
            DuplicateCheckPassed = false;
            DirectoryTree = null;
            return;
        }

        if (step <= 2)
        {
            EnvironmentSelected = false;
            NodeCheckPassed = false;
            ProjectFilesCreated = false;
            ProjectFilesCheckPassed = false;
            AgentUpJsonActionTaken = false;
            AgentUpJsonCheckPassed = false;
            StartCommandSucceeded = false;
            WorkspaceCheckPassed = false;
            DuplicateActionTaken = false;
            DuplicateCheckPassed = false;
            DirectoryTree = null;
            return;
        }

        if (step <= 3)
        {
            NodeCheckPassed = false;
            ProjectFilesCreated = false;
            ProjectFilesCheckPassed = false;
            AgentUpJsonActionTaken = false;
            AgentUpJsonCheckPassed = false;
            StartCommandSucceeded = false;
            WorkspaceCheckPassed = false;
            DuplicateActionTaken = false;
            DuplicateCheckPassed = false;
            DirectoryTree = null;
            return;
        }

        if (step <= 4)
        {
            ProjectFilesCreated = false;
            ProjectFilesCheckPassed = false;
            AgentUpJsonActionTaken = false;
            AgentUpJsonCheckPassed = false;
            StartCommandSucceeded = false;
            WorkspaceCheckPassed = false;
            DuplicateActionTaken = false;
            DuplicateCheckPassed = false;
            DirectoryTree = null;
            return;
        }

        if (step <= 5)
        {
            AgentUpJsonActionTaken = false;
            AgentUpJsonCheckPassed = false;
            StartCommandSucceeded = false;
            WorkspaceCheckPassed = false;
            DuplicateActionTaken = false;
            DuplicateCheckPassed = false;
            return;
        }

        if (step <= 6)
        {
            StartCommandSucceeded = false;
            WorkspaceCheckPassed = false;
            DuplicateActionTaken = false;
            DuplicateCheckPassed = false;
            return;
        }

        DuplicateActionTaken = false;
        DuplicateCheckPassed = false;
    }

    private static string? BuildDirectoryTree(string projectDirectory)
    {
        if (string.IsNullOrWhiteSpace(projectDirectory)) return null;
        var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(projectDirectory.Trim()));
        if (!Directory.Exists(root)) return null;

        var lines = new List<string> { $"{Path.GetFileName(root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))}/" };
        AppendDirectory(lines, root, "", 0);
        return string.Join(Environment.NewLine, lines);
    }

    private static void AppendDirectory(List<string> lines, string directory, string indent, int depth)
    {
        if (depth >= 3) return;

        var entries = Directory.EnumerateDirectories(directory)
            .Select(path => (Path: path, Name: Path.GetFileName(path), IsDirectory: true))
            .Concat(Directory.EnumerateFiles(directory)
                .Select(path => (Path: path, Name: Path.GetFileName(path), IsDirectory: false)))
            .OrderBy(entry => entry.IsDirectory ? 0 : 1)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .ToList();

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            var isLast = i == entries.Count - 1;
            var branch = isLast ? "`-- " : "|-- ";
            lines.Add($"{indent}{branch}{entry.Name}{(entry.IsDirectory ? "/" : "")}");
            if (entry.IsDirectory)
                AppendDirectory(lines, entry.Path, indent + (isLast ? "    " : "|   "), depth + 1);
        }
    }
}

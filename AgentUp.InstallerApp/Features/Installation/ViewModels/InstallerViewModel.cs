using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AgentUp.Installers.Features.Execution;
using AgentUp.Installers.Features.Flow;
using AgentUp.Installers.Features.Payloads;
using AgentUp.Installers.Features.Prerequisites;

namespace AgentUp.InstallerApp.Features.Installation.ViewModels;

public sealed class InstallerViewModel : INotifyPropertyChanged
{
    private readonly IInstallerPlatformAdapter _adapter;
    private InstallerSession _session;
    private bool _isBusy;
    private string _progressText = "";

    public InstallerViewModel(InstallerSession session, IInstallerPlatformAdapter adapter)
    {
        _session = session;
        _adapter = adapter;
        BackCommand = new DelegateCommand(_ => MoveBack(), _ => CanGoBack);
        NextCommand = new DelegateCommand(async _ => await MoveNextAsync(), _ => CanGoNext);
        AcceptLicenseCommand = new DelegateCommand(value => LicenseAccepted = value is true);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand BackCommand { get; }

    public ICommand NextCommand { get; }

    public ICommand AcceptLicenseCommand { get; }

    public InstallerStep Step => _session.Step;

    public string StepTitle => Step switch
    {
        InstallerStep.Welcome => "Welcome",
        InstallerStep.License => "License agreement",
        InstallerStep.Prerequisites => "Prerequisite validation",
        InstallerStep.Docker => "Docker status",
        InstallerStep.Components => "Component selection",
        InstallerStep.Location => "Installation location",
        InstallerStep.ServerConfiguration => "Server configuration",
        InstallerStep.Payload => "Release payload",
        InstallerStep.Summary => "Installation summary",
        InstallerStep.Progress => "Installing",
        InstallerStep.Completion => _session.ValidationReport?.Succeeded == true ? "Installation complete" : "Installation failed",
        _ => Step.ToString()
    };

    public string BodyText => Step switch
    {
        InstallerStep.Welcome => "Install Agent-Up Desktop, CLI, local Server service, and native launcher integration.",
        InstallerStep.License => "Review and accept the Agent-Up license terms before continuing.",
        InstallerStep.Prerequisites => "The installer validates platform prerequisites before making system changes.",
        InstallerStep.Docker => _session.DockerStatus?.Detail ?? "Docker has not been checked yet.",
        InstallerStep.Components => $"Selected: {_session.Components}",
        InstallerStep.Location => $"Install root: {_session.Location.RootDirectory}",
        InstallerStep.ServerConfiguration => $"Local Server endpoint: {_session.ServerUrl}",
        InstallerStep.Payload => $"{_session.Payload.Description}. Offline installation uses the bundled payload.",
        InstallerStep.Summary => SummaryText(),
        InstallerStep.Progress => _progressText,
        InstallerStep.Completion => CompletionText(),
        _ => ""
    };

    public bool LicenseAccepted
    {
        get => _session.LicenseAccepted;
        set
        {
            _session = InstallerWorkflow.AcceptLicense(_session, value);
            RaiseAll();
        }
    }

    public bool ShowLicenseCheck => Step == InstallerStep.License;

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (_isBusy == value)
                return;
            _isBusy = value;
            RaiseAll();
        }
    }

    public bool CanGoBack => !IsBusy && InstallerWorkflow.CanGoBack(_session);

    public bool CanGoNext => !IsBusy && InstallerWorkflow.CanGoNext(_session);

    public string NextButtonText => Step == InstallerStep.Summary ? "Install" : Step == InstallerStep.Completion ? "Close" : "Next";

    public static InstallerViewModel CreateDryRun()
    {
        var version = new Version(0, 0, 0);
        return new InstallerViewModel(
            InstallerSession.CreateDefault("Agent-Up", version, DefaultInstallRoot(), PayloadSelection.Bundled(version)),
            new FakeInstallerPlatformAdapter(CurrentPlatformName()));
    }

    internal async Task MoveNextAsync()
    {
        if (!CanGoNext)
            return;

        if (Step == InstallerStep.Prerequisites)
        {
            IsBusy = true;
            _session = InstallerWorkflow.WithDockerStatus(_session, await _adapter.CheckDockerAsync());
            IsBusy = false;
        }

        if (Step == InstallerStep.Summary)
        {
            await InstallAsync();
            return;
        }

        _session = InstallerWorkflow.GoNext(_session);
        RaiseAll();
    }

    internal void MoveBack()
    {
        if (!CanGoBack)
            return;

        _session = InstallerWorkflow.GoBack(_session);
        RaiseAll();
    }

    private async Task InstallAsync()
    {
        IsBusy = true;
        _session = InstallerWorkflow.StartInstall(_session);
        _progressText = "Starting installation...";
        RaiseAll();

        await foreach (var progress in _adapter.ExecuteInstallAsync(_session))
        {
            _progressText = $"{progress.Message} ({progress.CompletedOperations}/{progress.TotalOperations})";
            RaiseAll();
        }

        var report = await _adapter.ValidateInstalledStateAsync(_session);
        _session = InstallerWorkflow.Complete(_session, report);
        IsBusy = false;
        RaiseAll();
    }

    private string SummaryText()
    {
        var plan = _adapter.PlanInstall(_session);
        var elevated = plan.Count(operation => operation.RequiresElevation);
        return $"Ready to install {_session.ProductName} {_session.Version}. {plan.Count} operations planned; {elevated} require elevation.";
    }

    private string CompletionText()
    {
        if (_session.ValidationReport is null)
            return "Installation has not completed.";

        return _session.ValidationReport.Succeeded
            ? "Agent-Up was installed and validated successfully."
            : string.Join(Environment.NewLine, _session.ValidationReport.Findings.Select(finding => finding.Message));
    }

    private static string DefaultInstallRoot()
    {
        if (OperatingSystem.IsWindows())
            return @"C:\Program Files\Agent-Up";
        if (OperatingSystem.IsMacOS())
            return "/Applications/Agent-Up.app";
        return "/opt/agent-up";
    }

    private static string CurrentPlatformName()
    {
        if (OperatingSystem.IsWindows())
            return "Windows dry run";
        if (OperatingSystem.IsMacOS())
            return "macOS dry run";
        return "Linux dry run";
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Step));
        OnPropertyChanged(nameof(StepTitle));
        OnPropertyChanged(nameof(BodyText));
        OnPropertyChanged(nameof(LicenseAccepted));
        OnPropertyChanged(nameof(ShowLicenseCheck));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(NextButtonText));
        (BackCommand as DelegateCommand)?.RaiseCanExecuteChanged();
        (NextCommand as DelegateCommand)?.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

internal sealed class DelegateCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Predicate<object?>? _canExecute;

    public DelegateCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
    {
        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

    public void Execute(object? parameter) => _execute(parameter);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

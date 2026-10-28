using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Git.Controllers;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;
using AgentUp.Desktop.Features.Workspaces.Controllers;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Interface.ViewModels;

/// <summary>
/// Which part of an agent's own page is on screen.
///
/// The transcript is not one of them here. On Desktop it is markup inside the classic shell, so
/// the command center would have to own a second copy of it rather than the same one arranged
/// differently; the Agent surface of this workspace is where it is read.
/// </summary>
public enum AgentSection
{
    Overview,
    Git,
}

/// <summary>
/// One agent's own page: their workspace, read from the inside.
///
/// The team says what each agent is doing; this says what they are doing it in. It reads the same
/// workspace the classic shell opens, through the same two slice boundaries, arranged around the
/// agent rather than around the repository.
/// </summary>
public sealed class AgentPageViewModel : ReactiveObject
{
    private readonly WorkspacesController _workspaces;
    private readonly GitController _git;
    private AgentSection _section = AgentSection.Overview;
    private string? _branch;
    private string? _head;
    private string? _changesError;
    private bool _readChanges;

    public AgentPageViewModel(TeamStanding standing, WorkspacesController workspaces, GitController git)
    {
        Standing = standing;
        _workspaces = workspaces;
        _git = git;
        ShowOverviewCommand = ReactiveCommand.Create(() => Section = AgentSection.Overview);
        ShowGitCommand = ReactiveCommand.Create(() => Section = AgentSection.Git);
    }

    public TeamStanding Standing { get; }

    /// <summary>What is running in this workspace while the agent works.</summary>
    public ObservableCollection<AgentApplicationRowViewModel> Applications { get; } = [];

    /// <summary>The facts about the work in hand, in the order the questions come.</summary>
    public ObservableCollection<AgentFactRowViewModel> Facts { get; } = [];

    /// <summary>The files the proposal changes.</summary>
    public ObservableCollection<AgentChangeRowViewModel> Changes { get; } = [];

    public string DisplayName => Standing.DisplayName;

    /// <summary>What the agent is, under its name: the role it was given and the account it acts as.</summary>
    public string Subtitle => AgentPageText.Subtitle(Standing);

    public bool HasGate => Standing.Kind is TeamStandingKind.WaitingOnYou or TeamStandingKind.Halted;

    public string GateLabel => Standing.Kind == TeamStandingKind.Halted ? "Halted" : "Waiting on you";

    public string Reason => Standing.Reason ?? string.Empty;

    public bool HasReason => Standing.Reason is { Length: > 0 };

    public string DiagnosticCode => Standing.DiagnosticCode ?? string.Empty;

    public bool HasDiagnosticCode => Standing.DiagnosticCode is { Length: > 0 };

    public bool HasProposal => Standing.Forge.HasProposal;

    public string ProposalLabel => HasProposal
        ? $"#{Standing.Forge.ProposalNumber} {Standing.Forge.ProposalTitle}"
        : string.Empty;

    public string ProposalDetail => Standing.Forge.Detail ?? "No proposal open on this workspace.";

    public string CheckSummary => AgentPageText.CheckSummary(Standing);

    public string FindingSummary => AgentPageText.FindingSummary(Standing);

    public string MergeState => Standing.Forge.MergeState ?? string.Empty;

    public bool HasMergeState => Standing.Forge.MergeState is { Length: > 0 };

    public bool HasFacts => Facts.Count > 0;

    public bool HasApplications => Applications.Count > 0;

    public bool HasChanges => Changes.Count > 0;

    public string ChangesHeading => $"CHANGES · {Changes.Count}";

    public string ApplicationsHeading => $"APPLICATIONS · {Applications.Count}";

    public string ChangesError => _changesError ?? string.Empty;

    public bool HasChangesError => _changesError is { Length: > 0 };

    /// <summary>The hint under an empty change list, which depends on whether the read has happened.</summary>
    public string EmptyChangesHint => _readChanges ? "The working tree is clean." : "Reading the working tree…";

    public AgentSection Section
    {
        get => _section;
        private set
        {
            this.RaiseAndSetIfChanged(ref _section, value);
            this.RaisePropertyChanged(nameof(ShowsOverview));
            this.RaisePropertyChanged(nameof(ShowsGit));
        }
    }

    public bool ShowsOverview => Section == AgentSection.Overview;

    public bool ShowsGit => Section == AgentSection.Git;

    public ReactiveCommand<Unit, AgentSection> ShowOverviewCommand { get; }

    public ReactiveCommand<Unit, AgentSection> ShowGitCommand { get; }

    /// <summary>
    /// Fills the page from the workspace and its working tree. A read that fails leaves that part
    /// saying so rather than emptying the page: the standing it was opened from is still true.
    /// </summary>
    public async Task ReadAsync(CancellationToken cancellationToken = default)
    {
        await ReadWorkspaceAsync(cancellationToken);
        await ReadChangesAsync(cancellationToken);
        ApplyFacts();
    }

    private async Task ReadWorkspaceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await _workspaces.GetByIdAsync(Standing.WorkspaceId, cancellationToken);
            _branch = workspace?.Branch;
            _head = workspace?.Commit;
            Applications.Clear();
            foreach (var application in workspace?.Applications ?? [])
                Applications.Add(new AgentApplicationRowViewModel(application));
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            Applications.Clear();
        }

        this.RaisePropertyChanged(nameof(HasApplications));
        this.RaisePropertyChanged(nameof(ApplicationsHeading));
    }

    private async Task ReadChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var tree = await _git.GetChangesAsync(Standing.WorkspaceId, cancellationToken);
            Changes.Clear();
            foreach (var file in AgentPageText.Files(tree))
                Changes.Add(new AgentChangeRowViewModel(file.Path, file.Status));
            _changesError = null;
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            Changes.Clear();
            _changesError = failure.Message;
        }

        _readChanges = true;
        this.RaisePropertyChanged(nameof(HasChanges));
        this.RaisePropertyChanged(nameof(ChangesHeading));
        this.RaisePropertyChanged(nameof(ChangesError));
        this.RaisePropertyChanged(nameof(HasChangesError));
        this.RaisePropertyChanged(nameof(EmptyChangesHint));
    }

    private void ApplyFacts()
    {
        Facts.Clear();
        foreach (var fact in AgentPageText.Facts(Standing, _branch, _head))
            Facts.Add(new AgentFactRowViewModel(fact.Label, fact.Value));
        this.RaisePropertyChanged(nameof(HasFacts));
    }
}

/// <summary>One fact about the work in hand: what is asked, and what answers it.</summary>
public sealed class AgentFactRowViewModel(string label, string value)
{
    public string Label { get; } = label;

    public string Value { get; } = value;
}

/// <summary>One application running in the agent's workspace.</summary>
public sealed class AgentApplicationRowViewModel
{
    public AgentApplicationRowViewModel(ApplicationDto application)
    {
        Name = application.Name;
        State = application.State;
        Address = AgentPageText.Address(application);
        IsRunning = string.Equals(application.State, "Running", StringComparison.OrdinalIgnoreCase);
    }

    public string Name { get; }

    public string State { get; }

    public string Address { get; }

    public bool IsRunning { get; }
}

/// <summary>One file the proposal changes.</summary>
public sealed class AgentChangeRowViewModel(string path, string status)
{
    public string Path { get; } = path;

    public string Mark { get; } = status.Length > 0 ? status[..1].ToUpperInvariant() : "?";
}

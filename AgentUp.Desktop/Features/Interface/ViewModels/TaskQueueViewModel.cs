using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Models;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Interface.ViewModels;

/// <summary>
/// The queue the agents work from, and the issues you can add to it.
///
/// This is the one place the work is decided. An issue on the host is only a candidate; it reaches
/// an agent when it is picked into this list, and it is worked in the order this list holds.
/// </summary>
public sealed class TaskQueueViewModel : ReactiveObject
{
    private readonly TaskQueueController _queue;
    /// <summary>Counts reads so a superseded one can tell it is no longer the newest.</summary>
    private int _reads;

    private bool _isLoading;
    private string? _errorMessage;
    private bool _hasRead;
    private IReadOnlyList<TeamMember> _team = [];
    private string? _offeringWorkspaceId;

    public TaskQueueViewModel(TaskQueueController queue)
    {
        _queue = queue;
        RefreshCommand = ReactiveCommand.CreateFromTask(() => ReadAsync());
    }

    /// <summary>The queue in the order it will be worked.</summary>
    public ObservableCollection<QueuedTaskRowViewModel> Queue { get; } = [];

    /// <summary>Open issues on the offering workspace's repository that are not queued yet.</summary>
    public ObservableCollection<OfferedIssueRowViewModel> Offered { get; } = [];

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

    public bool IsLoading
    {
        get => _isLoading;
        private set => this.RaiseAndSetIfChanged(ref _isLoading, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
    }

    public bool HasError => ErrorMessage is { Length: > 0 };

    public bool HasQueue => Queue.Count > 0;

    public bool HasOffered => Offered.Count > 0;

    public string EmptyQueueHint => _hasRead
        ? "Nothing is queued, so the agents have nothing to take. Pick an issue below."
        : "Reading the queue…";

    public string EmptyOfferedHint => _offeringWorkspaceId is null
        ? "Open a workspace to see the issues on its repository."
        : "Every open issue on this repository is already queued.";

    /// <summary>
    /// The team, so a row can name the workspace holding or targeted by a task rather than show
    /// its id, and so the issues offered come from a workspace that actually has a repository.
    /// </summary>
    public void Attach(IReadOnlyList<TeamMember> team)
    {
        _team = team;
        _offeringWorkspaceId = team.FirstOrDefault()?.WorkspaceId;
    }

    public async Task ReadAsync(CancellationToken cancellationToken = default)
    {
        var read = ++_reads;
        IsLoading = true;
        ErrorMessage = null;
        try
        {
            var queued = await _queue.ListAsync(cancellationToken);
            var offered = _offeringWorkspaceId is null
                ? []
                : await _queue.ListIssuesAsync(_offeringWorkspaceId, cancellationToken);
            if (read != _reads) return;
            _hasRead = true;
            Apply(queued, offered);
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            if (read != _reads) return;
            ErrorMessage = failure.Message;
            this.RaisePropertyChanged(nameof(HasError));
        }
        finally
        {
            if (read == _reads) IsLoading = false;
        }
    }

    private void Apply(IReadOnlyList<QueuedTask> queued, IReadOnlyList<OfferedIssue> offered)
    {
        Queue.Clear();
        foreach (var task in queued)
            Queue.Add(new QueuedTaskRowViewModel(task, _team, this));

        // An issue already in the queue is not offered again: picking it twice would change
        // nothing, and showing it invites the click that does nothing.
        var queuedIds = queued.Select(task => task.Id).ToHashSet(StringComparer.Ordinal);
        Offered.Clear();
        foreach (var issue in offered.Where(issue => !queuedIds.Contains(issue.Id)))
            Offered.Add(new OfferedIssueRowViewModel(issue, this));

        this.RaisePropertyChanged(nameof(HasQueue));
        this.RaisePropertyChanged(nameof(HasOffered));
        this.RaisePropertyChanged(nameof(EmptyQueueHint));
        this.RaisePropertyChanged(nameof(EmptyOfferedHint));
    }

    internal async Task PickAsync(OfferedIssue issue)
    {
        if (_offeringWorkspaceId is null) return;
        await Run(token => _queue.EnqueueAsync(_offeringWorkspaceId, issue.Id, null, token));
    }

    internal Task RemoveAsync(QueuedTask task) => Run(token => _queue.RemoveAsync(task, token));

    internal Task ReleaseAsync(QueuedTask task) => Run(token => _queue.ReleaseAsync(task, token));

    internal Task MoveAsync(QueuedTask task, int delta)
    {
        var index = Queue.ToList().FindIndex(row => row.Id == task.Id);
        if (index < 0) return Task.CompletedTask;
        return Run(token => _queue.MoveAsync(task, index + delta, token));
    }

    /// <summary>
    /// Runs one change and re-reads. The Server owns the queue, so the list is re-read rather than
    /// edited here: a change that the Server refused must not leave the screen saying it happened.
    /// </summary>
    private async Task Run(Func<CancellationToken, Task> change)
    {
        string? refused = null;
        try
        {
            await change(CancellationToken.None);
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            refused = failure.Message;
        }

        await ReadAsync();

        // After the re-read, not before: the read clears the error it did not cause, so saying
        // why a change was refused first would flash the message and wipe it in the same breath.
        if (refused is null) return;
        ErrorMessage = refused;
        this.RaisePropertyChanged(nameof(HasError));
    }
}

/// <summary>One task in the queue, with who may take it and who has it.</summary>
public sealed class QueuedTaskRowViewModel
{
    private readonly QueuedTask _task;
    private readonly IReadOnlyList<TeamMember> _team;

    public QueuedTaskRowViewModel(QueuedTask task, IReadOnlyList<TeamMember> team, TaskQueueViewModel queue)
    {
        _task = task;
        _team = team;
        RemoveCommand = ReactiveCommand.CreateFromTask(() => queue.RemoveAsync(task));
        ReleaseCommand = ReactiveCommand.CreateFromTask(() => queue.ReleaseAsync(task));
        MoveUpCommand = ReactiveCommand.CreateFromTask(() => queue.MoveAsync(task, -1));
        MoveDownCommand = ReactiveCommand.CreateFromTask(() => queue.MoveAsync(task, 1));
    }

    public string Id => _task.Id;

    public string TaskLabel => $"#{_task.Number} · {_task.Title}";

    public string RepositoryLabel => $"{_task.Owner}/{_task.Repository}";

    /// <summary>Who may take it: one agent by name, or anyone on the repository.</summary>
    public string TargetLabel => _task.TargetWorkspaceId is null ? "anyone" : Name(_task.TargetWorkspaceId);

    /// <summary>Who has it now. A free task says so rather than leaving the column blank.</summary>
    public string HolderLabel => _task.HeldByWorkspaceId is null ? "free" : Name(_task.HeldByWorkspaceId);

    public bool IsHeld => _task.HeldByWorkspaceId is not null;

    public ReactiveCommand<Unit, Unit> RemoveCommand { get; }

    public ReactiveCommand<Unit, Unit> ReleaseCommand { get; }

    public ReactiveCommand<Unit, Unit> MoveUpCommand { get; }

    public ReactiveCommand<Unit, Unit> MoveDownCommand { get; }

    private string Name(string workspaceId) =>
        _team.FirstOrDefault(member => member.WorkspaceId == workspaceId)?.DisplayName ?? workspaceId;
}

/// <summary>One open issue that is not queued yet.</summary>
public sealed class OfferedIssueRowViewModel
{
    private readonly OfferedIssue _issue;

    public OfferedIssueRowViewModel(OfferedIssue issue, TaskQueueViewModel queue)
    {
        _issue = issue;
        PickCommand = ReactiveCommand.CreateFromTask(() => queue.PickAsync(issue));
    }

    public string IssueLabel => $"#{_issue.Number} · {_issue.Title}";

    public ReactiveCommand<Unit, Unit> PickCommand { get; }
}

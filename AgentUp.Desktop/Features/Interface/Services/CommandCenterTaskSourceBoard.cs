using System.Reactive.Linq;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.ViewModels;

namespace AgentUp.Desktop.Features.Interface.Services;

/// <summary>
/// The Tasks page's forge connections and the repository currently open on that page.
/// The view holds one of these and applies it; it does not decide the rows or the host URL.
/// Each team member has their own token; the board remembers which member's Settings tab is open.
/// </summary>
public sealed class CommandCenterTaskSourceBoard
{
    public const int RefreshSeconds = 30;

    public string AuthTab { get; private set; } = "";

    public string ForgeTab { get; private set; } = "github";

    public string TasksTab { get; private set; } = "issues";

    public string? OpenModule { get; private set; }

    public string? OpenOwner { get; private set; }

    public string? OpenName { get; private set; }

    public IReadOnlyList<TaskSourceRepository> Repositories { get; private set; } = [];

    public IReadOnlyList<CommandCenterTaskSlot> DevPulls { get; private set; } = [];

    public IReadOnlyList<CommandCenterTaskSlot> PmPulls { get; private set; } = [];

    public bool SourceRequested { get; private set; }

    public string? OpenRepositoryLabel => OpenOwner is null ? null : $"{OpenOwner}/{OpenName}";

    public IReadOnlyList<CommandCenterTaskSlot> VisibleDevPulls => OpenOwner is null ? [] : DevPulls;

    public IReadOnlyList<CommandCenterTaskSlot> VisiblePmPulls => OpenOwner is null ? [] : PmPulls;

    public IReadOnlyList<CommandCenterRepositorySlot> RepositorySlots()
        => Repositories.Select(repository => new CommandCenterRepositorySlot(
            $"{repository.ModuleId}/{repository.Owner}/{repository.Name}",
            $"{repository.Owner}/{repository.Name}",
            repository.ModuleId == "forgejo" ? "Forgejo" : "GitHub",
            (repository.Members ?? []).Select(member => new CommandCenterMemberSlot(
                member.WorkspaceId,
                member.DisplayName,
                member.Initials)).ToArray())).ToArray();

    public void ShowAuthTab(string? id)
    {
        if (!string.IsNullOrEmpty(id))
            AuthTab = id;
    }

    public void ShowForgeTab(string? id)
    {
        if (!string.IsNullOrEmpty(id))
            ForgeTab = id;
    }

    public void ShowTasksTab(string tab) => TasksTab = tab;

    public void CloseRepository(TaskQueueViewModel? tasks)
    {
        OpenModule = null;
        OpenOwner = null;
        OpenName = null;
        DevPulls = [];
        PmPulls = [];
        tasks?.SelectRepository(null, null, null);
    }

    public bool OpenRepository(string? id, TaskQueueViewModel? tasks)
    {
        var parsed = CommandCenterScene.ParseRepositoryKey(id);
        if (parsed is null)
            return false;
        OpenModule = parsed.Value.ModuleId;
        OpenOwner = parsed.Value.Owner;
        OpenName = parsed.Value.Name;
        TasksTab = "issues";
        tasks?.SelectRepository(OpenModule, OpenOwner, OpenName);
        return true;
    }

    public IReadOnlyList<CommandCenterTaskSlot> TasksOf(TaskQueueViewModel? tasks, IReadOnlyList<TeamStanding> team)
    {
        if (tasks is null || OpenOwner is null)
            return [];
        var repo = $"{OpenOwner}/{OpenName}";
        bool Mine(string label) => string.Equals(label, repo, StringComparison.OrdinalIgnoreCase);
        string Holder(string? id) => team.FirstOrDefault(standing => standing.WorkspaceId == id)?.DisplayName ?? "";
        string Face(string? workspaceId, string? login = null)
        {
            var name = workspaceId is { Length: > 0 } ? Holder(workspaceId) : null;
            var source = name is { Length: > 0 } ? name : login;
            return source is { Length: > 0 } ? TaskSourceMember.InitialsFrom(source) : "";
        }

        return
        [
            .. tasks.Working.Where(row => Mine(row.RepositoryLabel)).Select(row => new CommandCenterTaskSlot(
                row.Id, row.TaskLabel, row.RepositoryLabel, Holder(row.HeldByWorkspaceId), "stop", Face(row.HeldByWorkspaceId))),
            .. tasks.Queue.Where(row => Mine(row.RepositoryLabel)).Select(row => new CommandCenterTaskSlot(
                row.Id, row.TaskLabel, row.RepositoryLabel, "", "dequeue", Face(row.TargetWorkspaceId))),
            .. tasks.Offered.Select((row, index) => new CommandCenterTaskSlot(
                index.ToString(), row.IssueLabel, repo, "", "queue", Face(null, row.AssigneeLogin))),
        ];
    }

    public async Task EnsureSourceAsync(TaskSourceViewModel? source)
    {
        if (SourceRequested || source is null)
            return;
        await RefreshSourceAsync(source);
    }

    public async Task RefreshSourceAsync(TaskSourceViewModel? source)
    {
        if (source is null)
            return;
        SourceRequested = true;
        await source.LoadAsync();
        await source.LoadRepositoriesAsync();
        Repositories = source.Repositories;
    }

    public async Task SaveTokenAsync(
        TaskSourceViewModel? source,
        string workspaceId,
        IReadOnlyDictionary<string, string>? fields,
        string? text)
    {
        var (token, endpoint) = CommandCenterScene.TokenSave(fields, text);
        if (source is null || workspaceId.Length == 0 || token.Length == 0)
            return;
        await source.SaveAsync(workspaceId, ForgeTab, endpoint, token);
        Repositories = source.Repositories;
    }

    public async Task ForgetTokenAsync(TaskSourceViewModel? source, string workspaceId)
    {
        if (source is null || workspaceId.Length == 0)
            return;
        await source.ForgetAsync(workspaceId);
        Repositories = source.Repositories;
    }

    public async Task LoadOpenRepositoryAsync(TaskQueueViewModel? tasks, TaskSourceViewModel? source)
    {
        if (tasks is not null)
            await tasks.ReadAsync();
        if (source is null || OpenModule is null || OpenOwner is null || OpenName is null)
        {
            DevPulls = [];
            PmPulls = [];
            return;
        }

        var module = OpenModule;
        var owner = OpenOwner;
        var name = OpenName;
        try
        {
            var pulls = await source.PullsAsync(module, owner, name);
            if (OpenModule != module || OpenOwner != owner || OpenName != name)
                return;
            var repo = $"{owner}/{name}";
            DevPulls = CommandCenterScene.ReviewRows(pulls, "devReviewer", repo);
            PmPulls = CommandCenterScene.ReviewRows(pulls, "pmReviewer", repo);
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException)
        {
            DevPulls = [];
            PmPulls = [];
        }
    }

    public void PickOffered(TaskQueueViewModel? tasks, string? id)
    {
        if (int.TryParse(id, out var index) && tasks is not null && index >= 0 && index < tasks.Offered.Count)
            tasks.Offered[index].PickCommand.Execute().Subscribe();
    }
}

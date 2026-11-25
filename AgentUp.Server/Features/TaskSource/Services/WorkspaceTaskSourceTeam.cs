using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Forge.Controllers;
using AgentUp.Server.Features.Forge.DTOs;
using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.TaskSource.Services;

/// <summary>
/// The reviewer accounts on a repository, read from the workspaces checked out on it. An author
/// is not a reviewer. A workspace whose host account cannot be read is still counted as a kind,
/// so the review is owed even when the login that would match a pull request is missing.
/// </summary>
public sealed class WorkspaceTaskSourceTeam(
    ForgeController forge,
    WorkspaceQueryController workspaces,
    IAgentWorkspaceSettingsRepository settings) : ITaskSourceTeam
{
    public async Task<TaskSourceTeam> ForAsync(string owner, string repository, CancellationToken cancellationToken)
    {
        var kindByLogin = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var workspace in workspaces.GetAll())
        {
            var member = settings.Get(workspace.Id).Member;
            if (!TeamRoles.Reviews(member))
                continue;

            var bound = forge.ResolveRepository(workspace.Id);
            if (bound is null
                || !string.Equals(bound.Owner, owner, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(bound.Repository, repository, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var kind = member == TeamMemberKind.DevReviewer ? "devReviewer" : "pmReviewer";
            kinds.Add(kind);
            ForgeAccountDto? account;
            try
            {
                account = await forge.ReadAccountAsync(workspace.Id, cancellationToken);
            }
            catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException or InvalidOperationException)
            {
                account = null;
            }

            if (account is { Login.Length: > 0 })
                kindByLogin[account.Login] = kind;
        }

        return new TaskSourceTeam(kindByLogin, kinds);
    }
}

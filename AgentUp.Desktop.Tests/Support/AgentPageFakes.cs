using AgentUp.Desktop.Features.Git.Controllers;
using AgentUp.Desktop.Features.Git.Services;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.ViewModels;
using AgentUp.Desktop.Features.Workspaces.Controllers;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.Interfaces;
using AgentUp.Desktop.Features.Workspaces.Services;

namespace AgentUp.Desktop.Tests.Support;

/// <summary>
/// The workspace read an agent's own page makes, answered from a dictionary. Only the one read the
/// page performs is answered; the rest of the surface is not this fake's business.
/// </summary>
internal sealed class AgentPageWorkspaceFake : IWorkspaceApiProvider
{
    public Dictionary<string, WorkspaceDto?> Workspaces { get; } = [];

    public Dictionary<string, Exception> Failures { get; } = [];

    public Task<WorkspaceDto?> GetByIdAsync(string workspaceId, CancellationToken cancellationToken = default)
        => Failures.TryGetValue(workspaceId, out var failure)
            ? Task.FromException<WorkspaceDto?>(failure)
            : Task.FromResult(Workspaces.GetValueOrDefault(workspaceId));

    public Task<List<WorkspaceDto>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<List<WorkspaceDto>>([]);

    public Task<WorkspaceDto> CloneAsync(CloneSourceRequestDto request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<WorkspaceDto> RefreshCloneAsync(string workspaceId, string? revision = null, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task StartAsync(string workspaceId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task StopAsync(string workspaceId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task DeleteAsync(string workspaceId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<WorkspaceOverviewDto?> GetOverviewAsync(string workspaceId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<string?> GetReviewReportHtmlAsync(string workspaceId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

/// <summary>
/// Builds an agent's own page over the two reads behind it, so a test states the Server's answers
/// and nothing about how the page reaches them.
/// </summary>
internal static class AgentPageComposition
{
    public static WorkspacesController Workspaces(AgentPageWorkspaceFake fake)
        => new(new WorkspaceListService(fake));

    public static GitController Git(StandingForgeFake fake)
        => new(new GitChangeListService(fake));

    public static AgentPageViewModel Page(
        TeamStanding standing,
        AgentPageWorkspaceFake? workspaces = null,
        StandingForgeFake? git = null)
        => new(standing, Workspaces(workspaces ?? new AgentPageWorkspaceFake()), Git(git ?? new StandingForgeFake()));
}

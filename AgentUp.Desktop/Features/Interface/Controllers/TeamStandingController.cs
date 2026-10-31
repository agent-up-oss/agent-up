using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Services;

namespace AgentUp.Desktop.Features.Interface.Controllers;

public sealed class TeamStandingController
{
    private readonly TeamStandingService _standings;

    public TeamStandingController(TeamStandingService standings)
    {
        _standings = standings;
    }

    public Task<IReadOnlyList<TeamStanding>> ReadAsync(
        IReadOnlyList<TeamMember> workspaces,
        CancellationToken cancellationToken = default)
        => _standings.ReadAsync(workspaces, cancellationToken);
}

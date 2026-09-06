using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Database.Models;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Database.Models;

public sealed record DatabaseContext(
    Workspace Workspace,
    ApplicationInstance Application,
    DatabaseConnectionSettings Settings);

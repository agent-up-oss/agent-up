using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Workspaces.Models;

namespace AgentUp.Server.Features.Workspaces.DTOs;

public static class ApplicationReadinessDescription
{
    public static string For(ApplicationInstance app)
        => ApplicationReadiness.DiagnosticsSignal(app.LastError, ApplicationReadinessSignal.For(app));
}

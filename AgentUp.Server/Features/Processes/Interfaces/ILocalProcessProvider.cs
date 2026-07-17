using System.Diagnostics;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.Processes.Interfaces;

public interface ILocalProcessProvider
{
    Process CreateApplicationProcess(Workspace workspace, ApplicationInstance app);
    void Kill(Process process);
}

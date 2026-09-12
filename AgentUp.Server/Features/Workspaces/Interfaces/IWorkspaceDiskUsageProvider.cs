namespace AgentUp.Server.Features.Workspaces.Interfaces;

public interface IWorkspaceDiskUsageProvider
{
    long Measure(string path);
}

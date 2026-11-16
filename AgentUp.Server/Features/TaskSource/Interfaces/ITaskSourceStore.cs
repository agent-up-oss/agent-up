using AgentUp.Server.Features.TaskSource.Models;

namespace AgentUp.Server.Features.TaskSource.Interfaces;

/// <summary>Where the configured source survives a restart.</summary>
public interface ITaskSourceStore
{
    TaskSourceSetting? Read();

    void Write(TaskSourceSetting? source);
}

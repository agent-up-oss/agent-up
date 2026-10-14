namespace AgentUp.Server.Features.Agents.Interfaces;

public interface IAutonomySettings
{
    bool ServerEnabled { get; }

    bool TryEnable(out string? refusal);

    void Disable();
}

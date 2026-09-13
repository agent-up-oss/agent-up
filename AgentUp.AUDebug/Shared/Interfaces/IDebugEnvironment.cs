namespace AgentUp.AUDebug.Shared.Interfaces;

public interface IDebugEnvironment
{
    string? GetVariable(string name);
    string Display { get; }
    string? AdminPassword { get; }
    string? FindOnPath(string executableName);
}

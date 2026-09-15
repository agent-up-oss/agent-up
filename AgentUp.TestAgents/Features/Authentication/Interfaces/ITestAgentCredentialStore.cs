namespace AgentUp.TestAgents.Features.Authentication.Interfaces;

/// <summary>Where a test agent keeps the token it signed in for.</summary>
public interface ITestAgentCredentialStore
{
    string? Read();

    void Write(string token);
}

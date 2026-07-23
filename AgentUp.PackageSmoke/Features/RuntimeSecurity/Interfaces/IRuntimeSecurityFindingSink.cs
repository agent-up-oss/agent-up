namespace AgentUp.PackageSmoke.Features.RuntimeSecurity.Interfaces;

public interface IRuntimeSecurityFindingSink
{
    void Info(string code, string message);

    void Error(string code, string message);
}

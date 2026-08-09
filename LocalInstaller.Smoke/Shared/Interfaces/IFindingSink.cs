namespace AgentUp.PackageSmoke.Shared.Interfaces;

public interface IFindingSink
{
    void Info(string code, string message);

    void Error(string code, string message);
}

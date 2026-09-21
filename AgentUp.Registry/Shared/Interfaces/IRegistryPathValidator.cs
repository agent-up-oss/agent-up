namespace AgentUp.Registry.Shared.Interfaces;

public interface IRegistryPathValidator
{
    string RegistryRoot { get; }
    string ResolvePackageDirectory(string id, string version);
    string ResolveStagingDirectory(string id, string version);
    string ResolveIndexPath();
}

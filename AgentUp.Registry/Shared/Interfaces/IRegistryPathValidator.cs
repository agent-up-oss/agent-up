namespace AgentUp.Registry.Shared.Interfaces;

public interface IRegistryPathValidator
{
    string RegistryRoot { get; }
    string ResolvePackageDirectory(string id, string version);
    string StagingRoot { get; }
    string ResolveIndexPath();
    string RequireWithinRoot(string path);
}

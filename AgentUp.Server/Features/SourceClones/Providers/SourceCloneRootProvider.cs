using AgentUp.Server.Features.SourceClones.Interfaces;

namespace AgentUp.Server.Features.SourceClones.Providers;

public sealed class SourceCloneRootProvider : ISourceCloneRootProvider
{
    public const string RootEnvironmentVariable = "AGENTUP_SOURCE_CLONES_ROOT";
    public const string DefaultDirectoryName = "sources";

    private readonly string _dataDirectory;

    public SourceCloneRootProvider(string dataDirectory)
    {
        _dataDirectory = dataDirectory;
    }

    public string GetRoot()
    {
        var configured = Environment.GetEnvironmentVariable(RootEnvironmentVariable);
        var root = string.IsNullOrWhiteSpace(configured)
            ? Path.Join(_dataDirectory, DefaultDirectoryName)
            : configured.Trim();

        return Path.GetFullPath(root);
    }
}

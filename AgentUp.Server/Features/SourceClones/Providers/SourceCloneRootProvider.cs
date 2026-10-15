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

    public bool IsUnderRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var root = GetRoot();
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(root, fullPath);
        return relative != "."
               && relative != ".."
               && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
               && !Path.IsPathRooted(relative);
    }

    public void DeleteIfUnderRoot(string path)
    {
        if (!IsUnderRoot(path) || !Directory.Exists(path))
            return;

        Directory.Delete(path, recursive: true);
    }
}

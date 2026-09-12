namespace AgentUp.InstallerConfig.Tests.Support;

/// <summary>
/// A throwaway directory tree standing in for a checkout, so the upward search for .env is
/// exercised against real directories without moving the process's working directory.
/// </summary>
internal sealed class RepositoryTree : IDisposable
{
    private RepositoryTree(string root) => Root = root;

    /// <summary>The top of the tree, where a repository-level .env would sit.</summary>
    public string Root { get; }

    public static RepositoryTree Create()
    {
        var root = Path.Join(
            TestContext.CurrentContext.WorkDirectory, "dotenv-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return new RepositoryTree(root);
    }

    /// <summary>Creates a nested directory and returns it, standing in for a subproject.</summary>
    public string Subdirectory(params string[] segments)
    {
        var path = Path.Join([Root, .. segments]);
        Directory.CreateDirectory(path);
        return path;
    }

    public string WriteDotEnv(string directory, string content)
    {
        var path = Path.Join(directory, ".env");
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, recursive: true);
    }
}

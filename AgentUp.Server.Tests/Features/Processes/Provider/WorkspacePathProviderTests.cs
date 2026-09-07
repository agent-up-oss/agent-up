using AgentUp.Server.Shared.Providers;

namespace AgentUp.Server.Tests.Features.Processes.Provider;

[TestFixture]
public sealed class WorkspacePathProviderTests
{
    [Test]
    public void ResolveWorkspaceRootFile_AllowsNestedPathUnderWorkspaceRoot()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        var envDirectory = Path.Join(root, "Examples", "full-stack-react");
        Directory.CreateDirectory(envDirectory);
        var envPath = Path.Join(envDirectory, "database.env");
        File.WriteAllText(envPath, "POSTGRES_DB=agentup");

        try
        {
            var resolved = WorkspacePathProvider.ResolveWorkspaceRootFile(
                root,
                "Examples/full-stack-react/database.env",
                "Environment file");

            Assert.That(resolved, Is.EqualTo(envPath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void ResolveWorkspaceRootFile_RejectsAbsolutePath()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WorkspacePathProvider.ResolveWorkspaceRootFile("/tmp", "/etc/passwd", "Environment file"));

        Assert.That(ex!.Message, Does.Contain("must be relative to the workspace root"));
    }

    [Test]
    public void ResolveWorkspaceRootFile_RejectsPathOutsideWorkspaceRoot()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            WorkspacePathProvider.ResolveWorkspaceRootFile("/repo", "../.env", "Environment file"));

        Assert.That(ex!.Message, Does.Contain("must stay under the workspace root"));
    }
}

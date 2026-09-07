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
    public void ResolveWorkspaceRootFile_RejectsSymlinkThatEscapesWorkspaceRoot()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        var outside = Path.Join(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Join(outside, "database.env"), "POSTGRES_DB=agentup");
        Directory.CreateSymbolicLink(Path.Join(root, "linked"), outside);

        try
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                WorkspacePathProvider.ResolveWorkspaceRootFile(
                    root,
                    "linked/database.env",
                    "Environment file"));

            Assert.That(ex!.Message, Does.Contain("must stay under the workspace root"));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
            if (Directory.Exists(outside))
                Directory.Delete(outside, recursive: true);
        }
    }
}

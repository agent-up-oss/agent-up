using AgentUp.Packaging.Features.ReleaseArtifacts;
using AgentUp.Packaging.Features.ReleaseArtifacts.Models;

namespace AgentUp.Packaging.Tests.Features.ReleaseArtifacts;

[TestFixture]
public class RepositoryPathsTests
{
    [Test]
    public void FindRepositoryRoot_prefersConfiguredRepositoryRoot()
    {
        var root = CreateRepositoryRoot();
        var previous = Environment.GetEnvironmentVariable("AGENTUP_REPOSITORY_ROOT");

        try
        {
            Environment.SetEnvironmentVariable("AGENTUP_REPOSITORY_ROOT", Path.Join(root, "nested"));
            Directory.CreateDirectory(Path.Join(root, "nested"));

            Assert.That(RepositoryPaths.FindRepositoryRoot(), Is.EqualTo(root));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTUP_REPOSITORY_ROOT", previous);
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void FindRepositoryRoot_usesCurrentDirectoryBeforeExecutableBaseDirectory()
    {
        var root = CreateRepositoryRoot();
        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        var previous = Environment.GetEnvironmentVariable("AGENTUP_REPOSITORY_ROOT");

        try
        {
            Environment.SetEnvironmentVariable("AGENTUP_REPOSITORY_ROOT", null);
            Directory.CreateDirectory(Path.Join(root, "nested"));
            Directory.SetCurrentDirectory(Path.Join(root, "nested"));

            Assert.That(RepositoryPaths.FindRepositoryRoot(), Is.EqualTo(root));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
            Environment.SetEnvironmentVariable("AGENTUP_REPOSITORY_ROOT", previous);
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRepositoryRoot()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-RepositoryPathsTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Join(root, "agent-up.sln"), "");
        return root;
    }
}

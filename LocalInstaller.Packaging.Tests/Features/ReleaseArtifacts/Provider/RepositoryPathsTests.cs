using LocalInstaller.Packaging.Features.ReleaseArtifacts.Models;

namespace LocalInstaller.Packaging.Tests.Features.ReleaseArtifacts.Provider;

[TestFixture]
public class RepositoryPathsTests
{
    [Test]
    public void FindRepositoryRoot_prefersConfiguredRepositoryRoot()
    {
        var root = CreateRepositoryRoot();
        var previous = Environment.GetEnvironmentVariable("LOCALINSTALLER_REPOSITORY_ROOT");

        try
        {
            Environment.SetEnvironmentVariable("LOCALINSTALLER_REPOSITORY_ROOT", Path.Join(root, "nested"));
            Directory.CreateDirectory(Path.Join(root, "nested"));

            Assert.That(RepositoryPaths.FindRepositoryRoot(), Is.EqualTo(root));
        }
        finally
        {
            Environment.SetEnvironmentVariable("LOCALINSTALLER_REPOSITORY_ROOT", previous);
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void FindRepositoryRoot_usesCurrentDirectoryBeforeExecutableBaseDirectory()
    {
        var root = CreateRepositoryRoot();
        var originalCurrentDirectory = Directory.GetCurrentDirectory();
        var previous = Environment.GetEnvironmentVariable("LOCALINSTALLER_REPOSITORY_ROOT");

        try
        {
            Environment.SetEnvironmentVariable("LOCALINSTALLER_REPOSITORY_ROOT", null);
            Directory.CreateDirectory(Path.Join(root, "nested"));
            Directory.SetCurrentDirectory(Path.Join(root, "nested"));

            Assert.That(RepositoryPaths.FindRepositoryRoot(), Is.EqualTo(root));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalCurrentDirectory);
            Environment.SetEnvironmentVariable("LOCALINSTALLER_REPOSITORY_ROOT", previous);
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateRepositoryRoot()
    {
        var root = Path.Join(Path.GetTempPath(), "AgentUp-RepositoryPathsTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(root);
        var saved = Directory.GetCurrentDirectory();
        try { Directory.SetCurrentDirectory(root); root = Directory.GetCurrentDirectory(); }
        finally { Directory.SetCurrentDirectory(saved); }
        File.WriteAllText(Path.Join(root, "localinstaller.sln"), "");
        return root;
    }
}

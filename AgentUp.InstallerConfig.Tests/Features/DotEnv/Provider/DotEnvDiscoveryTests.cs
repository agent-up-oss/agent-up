using AgentUp.InstallerConfig.Tests.Support;

namespace AgentUp.InstallerConfig.Tests.Features.DotEnv.Provider;

[TestFixture]
public sealed class DotEnvDiscoveryTests
{
    [Test]
    public void FindDotEnvFile_findsAFileInTheStartingDirectory()
    {
        using var tree = RepositoryTree.Create();
        var expected = tree.WriteDotEnv(tree.Root, "AGENTUP_PORT=5000\n");

        Assert.That(RepositoryDotEnv.FindDotEnvFile(tree.Root), Is.EqualTo(expected));
    }

    [Test]
    public void FindDotEnvFile_walksUpToTheRepositoryRoot()
    {
        // A tool started inside a subproject still has to see the checkout's .env.
        using var tree = RepositoryTree.Create();
        var expected = tree.WriteDotEnv(tree.Root, "AGENTUP_PORT=5000\n");
        var nested = tree.Subdirectory("AgentUp.Server", "bin", "Release");

        Assert.That(RepositoryDotEnv.FindDotEnvFile(nested), Is.EqualTo(expected));
    }

    [Test]
    public void FindDotEnvFile_prefersTheNearestFileOverOneFurtherUp()
    {
        using var tree = RepositoryTree.Create();
        tree.WriteDotEnv(tree.Root, "AGENTUP_PORT=5000\n");
        var nested = tree.Subdirectory("AgentUp.Server");
        var expected = tree.WriteDotEnv(nested, "AGENTUP_PORT=5100\n");

        Assert.That(RepositoryDotEnv.FindDotEnvFile(nested), Is.EqualTo(expected));
    }

    [Test]
    public void FindDotEnvFile_returnsNothingWhenNoFileExistsAnywhereAbove()
    {
        // Starts from the temp root rather than the repository, because the search walks
        // all the way up and this checkout may well have a .env of its own.
        var isolated = Path.Join(Path.GetTempPath(), "dotenv-absent-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        try
        {
            Assert.That(RepositoryDotEnv.FindDotEnvFile(isolated), Is.Null);
        }
        finally
        {
            Directory.Delete(isolated, recursive: true);
        }
    }

    [Test]
    public void LoadOptional_appliesAVariableThatIsNotAlreadySet()
    {
        using var tree = RepositoryTree.Create();
        var name = "AGENTUP_TEST_" + Guid.NewGuid().ToString("N");
        tree.WriteDotEnv(tree.Root, $"{name}=from-dotenv\n");
        try
        {
            RepositoryDotEnv.LoadOptional(tree.Root);

            Assert.That(Environment.GetEnvironmentVariable(name), Is.EqualTo("from-dotenv"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Test]
    public void LoadOptional_leavesAnAlreadySetVariableAlone()
    {
        // The real environment wins: an operator's export must not be overwritten by a
        // file that happens to be in the checkout.
        using var tree = RepositoryTree.Create();
        var name = "AGENTUP_TEST_" + Guid.NewGuid().ToString("N");
        tree.WriteDotEnv(tree.Root, $"{name}=from-dotenv\n");
        Environment.SetEnvironmentVariable(name, "already-set");
        try
        {
            RepositoryDotEnv.LoadOptional(tree.Root);

            Assert.That(Environment.GetEnvironmentVariable(name), Is.EqualTo("already-set"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    [Test]
    public void LoadOptional_doesNothingWhenThereIsNoFile()
    {
        var isolated = Path.Join(Path.GetTempPath(), "dotenv-none-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolated);
        try
        {
            Assert.That(() => RepositoryDotEnv.LoadOptional(isolated), Throws.Nothing);
        }
        finally
        {
            Directory.Delete(isolated, recursive: true);
        }
    }

    [Test]
    public void LoadOptional_reportsTheFileItRejected()
    {
        using var tree = RepositoryTree.Create();
        var path = tree.WriteDotEnv(tree.Root, "not an assignment\n");

        Assert.That(() => RepositoryDotEnv.LoadOptional(tree.Root),
            Throws.InstanceOf<InvalidOperationException>().With.Message.Contains(path));
    }
}

using AgentUp.InstallerConfig;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class RepositoryDotEnvTests
{
    [Test]
    public void LoadOptional_AppliesUnsetVariablesFromRepositoryDotEnv()
    {
        var root = CreateTempDirectory();
        File.WriteAllText(Path.Join(root, ".env"), "AGENTUP_ADMIN_PASSWORD=from-dotenv\n");
        var previousDirectory = Directory.GetCurrentDirectory();
        var previousValue = Environment.GetEnvironmentVariable("AGENTUP_ADMIN_PASSWORD");
        try
        {
            Directory.SetCurrentDirectory(root);
            Environment.SetEnvironmentVariable("AGENTUP_ADMIN_PASSWORD", null);
            RepositoryDotEnv.LoadOptional();
            Assert.That(Environment.GetEnvironmentVariable("AGENTUP_ADMIN_PASSWORD"), Is.EqualTo("from-dotenv"));
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            Environment.SetEnvironmentVariable("AGENTUP_ADMIN_PASSWORD", previousValue);
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void LoadOptional_DoesNotOverrideExistingEnvironmentVariables()
    {
        var root = CreateTempDirectory();
        File.WriteAllText(Path.Join(root, ".env"), "AGENTUP_ADMIN_PASSWORD=from-dotenv\n");
        var previousDirectory = Directory.GetCurrentDirectory();
        var previousValue = Environment.GetEnvironmentVariable("AGENTUP_ADMIN_PASSWORD");
        try
        {
            Directory.SetCurrentDirectory(root);
            Environment.SetEnvironmentVariable("AGENTUP_ADMIN_PASSWORD", "already-set");
            RepositoryDotEnv.LoadOptional();
            Assert.That(Environment.GetEnvironmentVariable("AGENTUP_ADMIN_PASSWORD"), Is.EqualTo("already-set"));
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
            Environment.SetEnvironmentVariable("AGENTUP_ADMIN_PASSWORD", previousValue);
            Directory.Delete(root, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Join(Path.GetTempPath(), $"agentup-dotenv-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}

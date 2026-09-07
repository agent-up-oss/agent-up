using AgentUp.CommitPolicy.Features.CommitPolicy.Providers;

namespace AgentUp.CommitPolicy.Tests.Features.CommitPolicy.Provider;

[TestFixture]
public sealed class CommitsConfigurationLoaderTests
{
    private string _repoRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _repoRoot = Path.Join(Path.GetTempPath(), "AgentUp-CommitsConfigurationLoaderTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_repoRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_repoRoot))
            Directory.Delete(_repoRoot, recursive: true);
    }

    [Test]
    public void Load_returnsEmptyWhenAgentUpJsonIsMissing()
    {
        var config = CommitsConfigurationLoader.Load(_repoRoot);

        Assert.That(config.Build, Is.Empty);
        Assert.That(config.Test, Is.Empty);
        Assert.That(config.Projects, Is.Empty);
    }

    [Test]
    public void Load_returnsEmptyWhenCommitsSectionIsAbsent()
    {
        File.WriteAllText(Path.Join(_repoRoot, "agent-up.json"), """{ "name": "Sample" }""");

        var config = CommitsConfigurationLoader.Load(_repoRoot);

        Assert.That(config.Build, Is.Empty);
        Assert.That(config.Test, Is.Empty);
        Assert.That(config.Projects, Is.Empty);
    }

    [Test]
    public void Load_parsesBuildTestAndProjectDefinitions()
    {
        File.WriteAllText(Path.Join(_repoRoot, "agent-up.json"), """
            {
              "name": "Sample",
              "commits": {
                "build": ["dotnet build agent-up.sln"],
                "test": ["dotnet test AgentUp.Architecture.Tests"],
                "projects": {
                  "AgentUp.CommitPolicy": { "test": ["dotnet test AgentUp.CommitPolicy.Tests"] },
                  "AgentUp.Server": { "test": ["dotnet test AgentUp.Server.Tests"], "dependsOn": ["AgentUp.CommitPolicy"] }
                }
              }
            }
            """);

        var config = CommitsConfigurationLoader.Load(_repoRoot);

        Assert.That(config.Build, Is.EqualTo(new[] { "dotnet build agent-up.sln" }));
        Assert.That(config.Test, Is.EqualTo(new[] { "dotnet test AgentUp.Architecture.Tests" }));
        Assert.That(config.Projects.Keys, Is.EquivalentTo(new[] { "AgentUp.CommitPolicy", "AgentUp.Server" }));
        Assert.That(config.Projects["AgentUp.Server"].Test, Is.EqualTo(new[] { "dotnet test AgentUp.Server.Tests" }));
        Assert.That(config.Projects["AgentUp.Server"].DependsOn, Is.EqualTo(new[] { "AgentUp.CommitPolicy" }));
        Assert.That(config.Projects["AgentUp.CommitPolicy"].DependsOn, Is.Empty);
    }

    [Test]
    public void Load_returnsEmptyForMalformedJson()
    {
        File.WriteAllText(Path.Join(_repoRoot, "agent-up.json"), "{ not valid json");

        var config = CommitsConfigurationLoader.Load(_repoRoot);

        Assert.That(config.Build, Is.Empty);
        Assert.That(config.Test, Is.Empty);
        Assert.That(config.Projects, Is.Empty);
    }
}

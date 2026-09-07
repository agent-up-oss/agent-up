using AgentUp.CommitPolicy.Features.CommitPolicy.Models;

namespace AgentUp.CommitPolicy.Tests.Features.CommitPolicy.Unit;

[TestFixture]
public sealed class CommitsConfigurationTests
{
    [Test]
    public void Constructor_PreservesBuildTestAndProjectValues()
    {
        var projects = new Dictionary<string, CommitsProjectConfiguration>
        {
            ["AgentUp.Server"] = new(["dotnet test AgentUp.Server.Tests"], ["AgentUp.CommitPolicy"])
        };

        var config = new CommitsConfiguration(["dotnet build agent-up.sln"], ["dotnet test AgentUp.Architecture.Tests"], projects);

        Assert.Multiple(() =>
        {
            Assert.That(config.Build, Is.EqualTo(new[] { "dotnet build agent-up.sln" }));
            Assert.That(config.Test, Is.EqualTo(new[] { "dotnet test AgentUp.Architecture.Tests" }));
            Assert.That(config.Projects, Is.SameAs(projects));
        });
    }

    [Test]
    public void Empty_hasNoBuildTestOrProjectCommands()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CommitsConfiguration.Empty.Build, Is.Empty);
            Assert.That(CommitsConfiguration.Empty.Test, Is.Empty);
            Assert.That(CommitsConfiguration.Empty.Projects, Is.Empty);
        });
    }

    [Test]
    public void ProjectConfiguration_PreservesTestAndDependsOnValues()
    {
        var project = new CommitsProjectConfiguration(["dotnet test AgentUp.CLI.Tests"], ["AgentUp.CommitPolicy"]);

        Assert.Multiple(() =>
        {
            Assert.That(project.Test, Is.EqualTo(new[] { "dotnet test AgentUp.CLI.Tests" }));
            Assert.That(project.DependsOn, Is.EqualTo(new[] { "AgentUp.CommitPolicy" }));
        });
    }
}

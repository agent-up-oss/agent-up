using AgentUp.CommitPolicy.Features.CommitPolicy.Models;
using AgentUp.CommitPolicy.Features.CommitPolicy.Providers;

namespace AgentUp.CommitPolicy.Tests.Features.CommitPolicy.Provider;

[TestFixture]
public sealed class CommitBuildPlanProviderTests
{
    private readonly CommitBuildPlanProvider _provider = new();

    [Test]
    public void ResolveCommands_returnsOnlyGeneralCommandsWhenNoProjectMatches()
    {
        var config = new CommitsConfiguration(
            ["dotnet build agent-up.sln"],
            ["dotnet test AgentUp.Architecture.Tests"],
            new Dictionary<string, CommitsProjectConfiguration>
            {
                ["AgentUp.Server"] = new(["dotnet test AgentUp.Server.Tests"], [])
            });

        var commands = _provider.ResolveCommands(config, ["docs/user-docs/setup.md"]);

        Assert.That(commands, Is.EqualTo(new[]
        {
            "dotnet build agent-up.sln",
            "dotnet test AgentUp.Architecture.Tests"
        }));
    }

    [Test]
    public void ResolveCommands_includesTouchedProjectTests()
    {
        var config = new CommitsConfiguration(
            ["dotnet build agent-up.sln"],
            ["dotnet test AgentUp.Architecture.Tests"],
            new Dictionary<string, CommitsProjectConfiguration>
            {
                ["AgentUp.Server"] = new(["dotnet test AgentUp.Server.Tests"], [])
            });

        var commands = _provider.ResolveCommands(config, ["AgentUp.Server/Features/Commits/Services/CommitsService.cs"]);

        Assert.That(commands, Is.EqualTo(new[]
        {
            "dotnet build agent-up.sln",
            "dotnet test AgentUp.Architecture.Tests",
            "dotnet test AgentUp.Server.Tests"
        }));
    }

    [Test]
    public void ResolveCommands_includesTransitiveDependentProjectTests()
    {
        var config = new CommitsConfiguration(
            [],
            [],
            new Dictionary<string, CommitsProjectConfiguration>
            {
                ["AgentUp.CommitPolicy"] = new(["dotnet test AgentUp.CommitPolicy.Tests"], []),
                ["AgentUp.Server"] = new(["dotnet test AgentUp.Server.Tests"], ["AgentUp.CommitPolicy"]),
                ["AgentUp.Desktop"] = new(["dotnet test AgentUp.Desktop.Tests"], ["AgentUp.Server"]),
                ["AgentUp.Mobile"] = new(["npm test"], ["AgentUp.Server"])
            });

        var commands = _provider.ResolveCommands(config, ["AgentUp.CommitPolicy/Features/CommitPolicy/Providers/CommitPolicyProvider.cs"]);

        Assert.That(commands, Is.EqualTo(new[]
        {
            "dotnet test AgentUp.CommitPolicy.Tests",
            "dotnet test AgentUp.Desktop.Tests",
            "npm test",
            "dotnet test AgentUp.Server.Tests"
        }));
    }

    [Test]
    public void ResolveCommands_deduplicatesCommandsSharedAcrossProjects()
    {
        var config = new CommitsConfiguration(
            ["dotnet build agent-up.sln"],
            [],
            new Dictionary<string, CommitsProjectConfiguration>
            {
                ["AgentUp.CLI"] = new(["dotnet build agent-up.sln"], [])
            });

        var commands = _provider.ResolveCommands(config, ["AgentUp.CLI/Features/Commits/Services/CommitsService.cs"]);

        Assert.That(commands, Is.EqualTo(new[] { "dotnet build agent-up.sln" }));
    }

    [Test]
    public void ResolveCommands_returnsEmptyForEmptyConfiguration()
    {
        var commands = _provider.ResolveCommands(CommitsConfiguration.Empty, ["AgentUp.Server/Features/Commits/Services/CommitsService.cs"]);

        Assert.That(commands, Is.Empty);
    }
}

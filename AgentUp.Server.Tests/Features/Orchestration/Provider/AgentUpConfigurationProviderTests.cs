using AgentUp.Server.Features.Orchestration.Providers;

namespace AgentUp.Server.Tests.Features.Orchestration.Provider;

/// <summary>
/// Starting a workspace re-registers it from this file, so what the Server reads here decides
/// which applications the workspace keeps. A Server with no capability packages enabled is the
/// ordinary case for an installed service, and it must still read the sections.
/// </summary>
[TestFixture]
public sealed class AgentUpConfigurationProviderTests
{
    private readonly List<string> _directories = [];

    [TearDown]
    public void TearDown()
    {
        foreach (var directory in _directories.Where(Directory.Exists))
            Directory.Delete(directory, true);
        _directories.Clear();
    }

    [Test]
    public async Task LoadAsync_reads_runtime_sections_when_no_capability_package_is_enabled()
    {
        var worktree = Worktree("""
            {
              "name": "Capability Workspace",
              "dotnet": [{ "name": "SmokeDotnet", "sdk": "10.0.x", "run": { "project": "SmokeDotnet/SmokeDotnet.csproj" } }],
              "docker": [{ "name": "SmokeDocker", "image": "nginx:alpine" }]
            }
            """);

        var config = await new AgentUpConfigurationProvider().LoadAsync(worktree, CancellationToken.None);

        var sections = config!.RuntimeSections ?? [];
        Assert.Multiple(() =>
        {
            Assert.That(sections.Select(section => section.ModuleId), Is.EquivalentTo(new[] { "dotnet", "docker" }));
            Assert.That((config.Dotnet ?? [])[0].Run.Project, Is.EqualTo("SmokeDotnet/SmokeDotnet.csproj"));
            Assert.That((config.Docker ?? [])[0].Image, Is.EqualTo("nginx:alpine"));
        });
    }

    [Test]
    public async Task LoadAsync_returns_null_when_the_worktree_has_no_agent_up_json()
    {
        var empty = Path.Join(Path.GetTempPath(), $"agent-up-config-{Guid.NewGuid():N}");
        _directories.Add(empty);
        Directory.CreateDirectory(empty);

        var config = await new AgentUpConfigurationProvider().LoadAsync(empty, CancellationToken.None);

        Assert.That(config, Is.Null);
    }

    /// <summary>A worktree holding just the agent-up.json this test needs.</summary>
    private string Worktree(string agentUpJson)
    {
        var directory = Path.Join(Path.GetTempPath(), $"agent-up-config-{Guid.NewGuid():N}");
        _directories.Add(directory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Join(directory, "agent-up.json"), agentUpJson);
        return directory;
    }
}

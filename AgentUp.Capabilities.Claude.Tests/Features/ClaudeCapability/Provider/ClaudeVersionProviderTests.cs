using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;

namespace AgentUp.Capabilities.Claude.Tests.Features.ClaudeCapability.Provider;

[TestFixture]
public sealed class ClaudeVersionProviderTests
{
    private string? _previousInventoryPath;

    [SetUp]
    public void SetUp()
    {
        _previousInventoryPath = Environment.GetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable);
        Environment.SetEnvironmentVariable(
            CapabilityInventoryFileProvider.InventoryPathVariable,
            Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.json"));
    }

    [TearDown]
    public void TearDown() =>
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, _previousInventoryPath);

    [Test]
    public async Task DiscoverAsync_usesInventoryDeclaredCommand()
    {
        var path = WriteInventory("""[{ "id": "claude", "versions": ["dev"], "command": "claude-agent-acp" }]""");
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, path);
        var commands = new RecordingCommandRunner();
        commands.Results[("claude-agent-acp", "--version")] = new CapabilityCommandResult(0, "0.5.0\n", "");

        var versions = await new ClaudeVersionProvider(Locator(commands)).DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Single(version => version.Location == "claude-agent-acp").Version, Is.EqualTo("0.5.0"));
    }

    [Test]
    public async Task DiscoverAsync_ignoresInteractiveCliWhenInventoryHasNoCommand()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("claude", "--version")] = new CapabilityCommandResult(0, "2.0.1\n", "");

        var versions = await new ClaudeVersionProvider(Locator(commands)).DiscoverAsync(CancellationToken.None);

        Assert.That(versions, Is.Empty);
    }

    [Test]
    public async Task ResolveLaunch_returnsTheInventoryCommandAfterDiscovery()
    {
        var path = WriteInventory("""[{ "id": "claude", "versions": ["dev"], "command": "claude-agent-acp" }]""");
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, path);
        var commands = new RecordingCommandRunner();
        commands.Results[("claude-agent-acp", "--version")] = new CapabilityCommandResult(0, "0.5.0\n", "");
        var provider = new ClaudeVersionProvider(Locator(commands));
        var versions = await provider.DiscoverAsync(CancellationToken.None);

        var launch = provider.ResolveLaunch(versions);

        Assert.That(launch.FileName, Is.EqualTo("claude-agent-acp"));
    }

    private static string WriteInventory(string json)
    {
        var path = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString(), "capabilities.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return path;
    }

    private static CapabilityCliLocator Locator(ICapabilityCommandRunner commands) =>
        new(commands, new FakeSearchPaths(), new FakeProbe(), "ubuntu");

    private sealed class RecordingCommandRunner : ICapabilityCommandRunner
    {
        public Dictionary<(string FileName, string Arguments), CapabilityCommandResult> Results { get; } = [];

        public Task<CapabilityCommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default) =>
            Task.FromResult(Results.GetValueOrDefault((fileName, string.Join(" ", arguments)), new CapabilityCommandResult(127, "", "missing")));
    }

    private sealed class FakeProbe : ICapabilityExecutableProbe
    {
        public bool IsExecutable(string path) => false;
    }

    private sealed class FakeSearchPaths : ICapabilitySearchPathProvider
    {
        public IReadOnlyList<string> Directories() => [];
    }
}

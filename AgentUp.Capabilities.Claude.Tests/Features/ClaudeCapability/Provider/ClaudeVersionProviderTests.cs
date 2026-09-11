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
    public async Task DiscoverAsync_findsClaudeAgentAcpAdapter()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("claude-agent-acp", "--version")] = new CapabilityCommandResult(0, "0.5.0\n", "");
        var locator = new CapabilityCliLocator(
            commands,
            new FakeSearchPaths(),
            new FakeProbe(),
            "ubuntu");

        var versions = await new ClaudeVersionProvider(locator).DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Single(version => version.Location == "claude-agent-acp").Version, Is.EqualTo("0.5.0"));
    }

    [Test]
    public void ResolveLaunch_usesClaudeAgentAcpBinary()
    {
        var locator = new CapabilityCliLocator(
            new RecordingCommandRunner(),
            new FakeSearchPaths(),
            new FakeProbe(),
            "ubuntu");
        var launch = new ClaudeVersionProvider(locator).ResolveLaunch([
            new("claude", "0.5.0", "claude-agent-acp", AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityVersionSource.System, false)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(launch.FileName, Is.EqualTo("claude-agent-acp"));
            Assert.That(launch.Arguments, Is.Empty);
        });
    }

    [Test]
    public async Task DiscoverAsync_doesNotTreatInteractiveClaudeCliAsAcp()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("claude", "--version")] = new CapabilityCommandResult(0, "2.0.1\n", "");
        var locator = new CapabilityCliLocator(
            commands,
            new FakeSearchPaths(),
            new FakeProbe(),
            "ubuntu");

        var versions = await new ClaudeVersionProvider(locator).DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Select(version => version.Location), Does.Not.Contain("claude"));
    }

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

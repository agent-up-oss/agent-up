using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Providers;

namespace AgentUp.Capabilities.Cursor.Tests.Features.CursorCapability.Provider;

[TestFixture]
public sealed class CursorVersionProviderTests
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
    public async Task DiscoverAsync_findsWellKnownAgentBinary()
    {
        var path = "/home/dev/.local/bin/agent";
        var locator = new CapabilityCliLocator(
            new RecordingCommandRunner(),
            new FakeSearchPaths("/home/dev/.local/bin"),
            new FakeProbe(path),
            "ubuntu");

        var versions = await new CursorVersionProvider(locator).DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Select(version => version.Location), Does.Contain(path));
    }

    [Test]
    public async Task DiscoverAsync_findsAgentOnPath()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("agent", "--version")] = new CapabilityCommandResult(0, "2026.01.09-abc\n", "");
        var locator = new CapabilityCliLocator(
            commands,
            new FakeSearchPaths(),
            new FakeProbe(),
            "ubuntu");

        var versions = await new CursorVersionProvider(locator).DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Single(version => version.Location == "agent").Version, Is.EqualTo("2026.01.09-abc"));
    }

    [Test]
    public async Task DiscoverAsync_findsCursorAgentAliasOnPath()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("cursor-agent", "--version")] = new CapabilityCommandResult(0, "2026.09.02-c22c1a3\n", "");
        var locator = new CapabilityCliLocator(
            commands,
            new FakeSearchPaths(),
            new FakeProbe(),
            "ubuntu");

        var versions = await new CursorVersionProvider(locator).DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Single(version => version.Location == "cursor-agent").Version, Is.EqualTo("2026.09.02-c22c1a3"));
    }

    private sealed class RecordingCommandRunner : ICapabilityCommandRunner
    {
        public Dictionary<(string FileName, string Arguments), CapabilityCommandResult> Results { get; } = [];

        public Task<CapabilityCommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default) =>
            Task.FromResult(Results.GetValueOrDefault((fileName, string.Join(" ", arguments)), new CapabilityCommandResult(127, "", "missing")));
    }

    private sealed class FakeProbe(params string[] paths) : ICapabilityExecutableProbe
    {
        public bool IsExecutable(string path) => paths.Contains(path, StringComparer.Ordinal);
    }

    private sealed class FakeSearchPaths(params string[] directories) : ICapabilitySearchPathProvider
    {
        public IReadOnlyList<string> Directories() => directories;
    }
}

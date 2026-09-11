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
    public async Task DiscoverAsync_usesInventoryDeclaredCommand()
    {
        var path = WriteInventory("""[{ "id": "cursor", "versions": ["dev"], "command": "agent", "arguments": ["acp"] }]""");
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, path);
        var commands = new RecordingCommandRunner();
        commands.Results[("agent", "--version")] = new CapabilityCommandResult(0, "2026.01.09-abc\n", "");

        var versions = await new CursorVersionProvider(Locator(commands, new FakeSearchPaths(), new FakeProbe())).DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Single(version => version.Location == "agent").Version, Is.EqualTo("2026.01.09-abc"));
    }

    [Test]
    public async Task DiscoverAsync_findsInventoryCommandInWellKnownDirectory()
    {
        var inventory = WriteInventory("""[{ "id": "cursor", "versions": ["dev"], "command": "agent", "arguments": ["acp"] }]""");
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, inventory);
        var agent = "/home/dev/.local/bin/agent";

        var versions = await new CursorVersionProvider(Locator(
            new RecordingCommandRunner(),
            new FakeSearchPaths("/home/dev/.local/bin"),
            new FakeProbe(agent))).DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Select(version => version.Location), Does.Contain(agent));
    }

    private static string WriteInventory(string json)
    {
        var path = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString(), "capabilities.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return path;
    }

    private static CapabilityCliLocator Locator(
        ICapabilityCommandRunner commands,
        ICapabilitySearchPathProvider searchPaths,
        ICapabilityExecutableProbe probe) =>
        new(commands, searchPaths, probe, "ubuntu");

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

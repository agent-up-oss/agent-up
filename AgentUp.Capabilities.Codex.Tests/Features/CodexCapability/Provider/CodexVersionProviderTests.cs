using AgentUp.Capabilities.Codex.Features.CodexCapability.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;

namespace AgentUp.Capabilities.Codex.Tests.Features.CodexCapability.Provider;

[TestFixture]
public sealed class CodexVersionProviderTests
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
        var path = WriteInventory("""[{ "id": "codex", "versions": ["dev"], "command": "codex-acp", "arguments": [] }]""");
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, path);
        var commands = new RecordingCommandRunner();
        commands.Results[("codex-acp", "--version")] = new CapabilityCommandResult(0, "0.16.0\n", "");

        var versions = await new CodexVersionProvider(Locator(commands)).DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Single(version => version.Location == "codex-acp").Version, Is.EqualTo("0.16.0"));
    }

    [Test]
    public async Task DiscoverAsync_ignoresInteractiveCliWhenInventoryHasNoCommand()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("codex", "--version")] = new CapabilityCommandResult(0, "codex-cli 0.40.0\n", "");

        var versions = await new CodexVersionProvider(Locator(commands)).DiscoverAsync(CancellationToken.None);

        Assert.That(versions, Is.Empty);
    }

    [Test]
    public async Task ResolveLaunch_usesInventoryArguments()
    {
        var path = WriteInventory("""[{ "id": "codex", "versions": ["dev"], "command": "/opt/codex-acp", "arguments": ["serve"] }]""");
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, path);
        var provider = new CodexVersionProvider(Locator(new RecordingCommandRunner()));
        await provider.DiscoverAsync(CancellationToken.None);

        var launch = provider.ResolveLaunch([
            new("codex", "dev", "/opt/codex-acp", AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityVersionSource.System, false)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(launch.FileName, Is.EqualTo("/opt/codex-acp"));
            Assert.That(launch.Arguments, Is.EqualTo(new[] { "serve" }));
        });
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

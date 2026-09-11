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
    public async Task DiscoverAsync_findsCodexAcpAdapter()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("codex-acp", "--version")] = new CapabilityCommandResult(0, "0.16.0\n", "");
        var versions = await new CodexVersionProvider(Locator(commands, "ubuntu")).DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Single(version => version.Location == "codex-acp").Version, Is.EqualTo("0.16.0"));
    }

    [Test]
    public async Task DiscoverAsync_doesNotTreatInteractiveCodexCliAsAcp()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("codex", "--version")] = new CapabilityCommandResult(0, "codex-cli 0.40.0\n", "");
        var versions = await new CodexVersionProvider(Locator(commands, "ubuntu")).DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Select(version => version.Location), Does.Not.Contain("codex"));
    }

    [Test]
    public void ResolveLaunch_usesCodexAcpBinary()
    {
        var launch = new CodexVersionProvider(Locator(new RecordingCommandRunner(), "ubuntu")).ResolveLaunch([
            new("codex", "0.16.0", "codex-acp", AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityVersionSource.System, false)
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(launch.FileName, Is.EqualTo("codex-acp"));
            Assert.That(launch.Arguments, Is.Empty);
        });
    }

    private static CapabilityCliLocator Locator(ICapabilityCommandRunner commands, string platform) =>
        new(commands, new FakeSearchPaths(), new FakeProbe(), platform);

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

using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

namespace AgentUp.Capabilities.Common.Tests.Features.CapabilityDiscovery.Provider;

[TestFixture]
public sealed class CapabilityCliLocatorTests
{
    [Test]
    public async Task DiscoverAsync_findsCandidateOnPath()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("agent", "--version")] = new CapabilityCommandResult(0, "0.16.0\n", "");
        var locator = Create(commands, new FakeExecutableProbe(), new FakeSearchPaths(), "ubuntu");

        var versions = await locator.DiscoverAsync("cursor", [Agent()], [], CancellationToken.None);

        Assert.That(versions.Single(version => version.Location == "agent").Version, Is.EqualTo("0.16.0"));
    }

    [Test]
    public async Task DiscoverAsync_readsTheFirstVersionTokenThatContainsADigit()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("agent", "--version")] = new CapabilityCommandResult(0, "codex-cli 0.40.0\n", "");
        var locator = Create(commands, new FakeExecutableProbe(), new FakeSearchPaths(), "ubuntu");

        var versions = await locator.DiscoverAsync("codex", [Agent()], [], CancellationToken.None);

        Assert.That(versions.Single(version => version.Location == "agent").Version, Is.EqualTo("0.40.0"));
    }

    [Test]
    public async Task DiscoverAsync_matchesTheExactWingetPackageId()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("winget", "list --id GitHub.Copilot")] = new CapabilityCommandResult(
            0,
            "GitHub.Copilot.Preview 9.9.9\nGitHub.Copilot 0.40.0\n",
            "");
        var locator = Create(commands, new FakeExecutableProbe(), new FakeSearchPaths(), "windows");

        var versions = await locator.DiscoverAsync(
            "copilot",
            [Agent()],
            [new("winget", ["list", "--id", "GitHub.Copilot"], "winget:GitHub.Copilot", "GitHub.Copilot", "windows")],
            CancellationToken.None);

        Assert.That(versions.Single(version => version.Location == "winget:GitHub.Copilot").Version, Is.EqualTo("0.40.0"));
    }

    [Test]
    public async Task DiscoverAsync_findsWellKnownExecutableWhenCommandIsNotOnPath()
    {
        var path = "/home/dev/.local/bin/agent";
        var locator = Create(
            new RecordingCommandRunner(),
            new FakeExecutableProbe(path),
            new FakeSearchPaths("/home/dev/.local/bin"),
            "ubuntu");

        var versions = await locator.DiscoverAsync("cursor", [Agent()], [], CancellationToken.None);

        Assert.That(versions.Select(version => version.Location), Does.Contain(path));
    }

    [Test]
    public async Task DiscoverAsync_detectsPackageManagerRecords()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("brew", "list --versions codex")] = new CapabilityCommandResult(0, "codex 0.40.1\n", "");
        var locator = Create(commands, new FakeExecutableProbe(), new FakeSearchPaths(), "macos");

        var versions = await locator.DiscoverAsync(
            "codex",
            [new("codex", ["--version"], ["acp"])],
            [new("brew", ["list", "--versions", "codex"], "brew:codex", "codex", "macos")],
            CancellationToken.None);

        Assert.That(versions.Single(version => version.Location == "brew:codex").Version, Is.EqualTo("0.40.1"));
    }

    [Test]
    public async Task DiscoverAsync_returnsNothingWhenNoCandidatesAreDeclared()
    {
        var locator = Create(new RecordingCommandRunner(), new FakeExecutableProbe(), new FakeSearchPaths(), "ubuntu");

        var versions = await locator.DiscoverAsync("codex", [], [], CancellationToken.None);

        Assert.That(versions, Is.Empty);
    }

    [Test]
    public async Task DiscoverAsync_deduplicatesCaseInsensitiveLocations()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("codex", "--version")] = new CapabilityCommandResult(0, "1.0.0\n", "");
        commands.Results[("CODEX", "--version")] = new CapabilityCommandResult(0, "1.0.0\n", "");
        var locator = Create(commands, new FakeExecutableProbe(), new FakeSearchPaths(), "ubuntu");

        var versions = await locator.DiscoverAsync(
            "codex",
            [new("codex", ["--version"], ["acp"]), new("CODEX", ["--version"], ["acp"])],
            [],
            CancellationToken.None);

        Assert.That(versions.Count(version => version.Version == "1.0.0"), Is.EqualTo(1));
    }

    [Test]
    public async Task DiscoverAsync_keepsACandidateWhoseFileNameHasNoFinalSegment()
    {
        var directory = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var location = Path.Join(directory, "foo") + Path.DirectorySeparatorChar;
        var locator = Create(
            new RecordingCommandRunner(),
            new FakeExecutableProbe(location),
            new FakeSearchPaths(directory),
            "ubuntu");

        var versions = await locator.DiscoverAsync(
            "codex",
            [new("foo/", ["--version"], ["acp"])],
            [],
            CancellationToken.None);

        Assert.That(versions.Select(version => version.Location), Does.Contain(location));
    }

    [Test]
    public void ResolveLaunch_returnsEmptyCommandWhenNoCandidatesAreDeclared()
    {
        var locator = Create(new RecordingCommandRunner(), new FakeExecutableProbe(), new FakeSearchPaths(), "ubuntu");

        var launch = locator.ResolveLaunch([], []);

        Assert.Multiple(() =>
        {
            Assert.That(launch.FileName, Is.Empty);
            Assert.That(launch.Arguments, Is.Empty);
        });
    }

    [Test]
    public void ResolveLaunch_prefersRootedPathAndMatchingArguments()
    {
        var locator = Create(new RecordingCommandRunner(), new FakeExecutableProbe(), new FakeSearchPaths(), "ubuntu");
        var launch = locator.ResolveLaunch(
            [Agent(), new("codex", ["--version"], ["acp"])],
            [
                new("cursor", "0.1.0", "brew:cursor-cli", AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityVersionSource.System, false),
                new("cursor", "unknown", "/home/dev/.local/bin/agent", AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityVersionSource.System, false)
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(launch.FileName, Is.EqualTo("/home/dev/.local/bin/agent"));
            Assert.That(launch.Arguments, Is.EqualTo(new[] { "acp" }));
        });
    }

    private static CapabilityCliCandidate Agent() => new("agent", ["--version"], ["acp"]);

    private static CapabilityCliLocator Create(
        ICapabilityCommandRunner commands,
        ICapabilityExecutableProbe probe,
        ICapabilitySearchPathProvider searchPaths,
        string platform) =>
        new(commands, searchPaths, probe, platform);

    private sealed class RecordingCommandRunner : ICapabilityCommandRunner
    {
        public Dictionary<(string FileName, string Arguments), CapabilityCommandResult> Results { get; } = [];

        public Task<CapabilityCommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            var key = (fileName, string.Join(" ", arguments));
            return Task.FromResult(Results.GetValueOrDefault(key, new CapabilityCommandResult(127, "", "missing")));
        }
    }

    private sealed class FakeExecutableProbe(params string[] paths) : ICapabilityExecutableProbe
    {
        public bool IsExecutable(string path) => paths.Contains(path, StringComparer.Ordinal);
    }

    private sealed class FakeSearchPaths(params string[] directories) : ICapabilitySearchPathProvider
    {
        public IReadOnlyList<string> Directories() => directories;
    }
}

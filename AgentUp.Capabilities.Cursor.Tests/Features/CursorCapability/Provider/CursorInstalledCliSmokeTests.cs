using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Providers;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

namespace AgentUp.Capabilities.Cursor.Tests.Features.CursorCapability.Provider;

[TestFixture]
[CancelAfter(60_000)]
public sealed class CursorInstalledCliSmokeTests
{
    [Test]
    public async Task DiscoverAsync_includesWellKnownExecutablesWhenTheyAreInstalled()
    {
        var versions = await Adapter().DiscoverAsync(TestContext.CurrentContext.CancellationToken);
        var locations = versions.Select(version => version.Location).ToArray();

        Assert.Multiple(() =>
        {
            foreach (var path in WellKnownExecutables())
                Assert.That(locations, Does.Contain(path), $"Installed Cursor CLI '{path}' was not discovered.");
            foreach (var version in versions)
            {
                Assert.That(version.CapabilityId, Is.EqualTo("cursor"));
                Assert.That(version.Version, Is.Not.WhiteSpace);
                Assert.That(version.Location, Is.Not.WhiteSpace);
            }
        });
    }

    [Test]
    public async Task ValidateAsync_matchesLiveDiscovery()
    {
        var adapter = Adapter();
        var installed = await adapter.DiscoverAsync(TestContext.CurrentContext.CancellationToken);
        var result = await adapter.ValidateAsync(Declaration(), installed, TestContext.CurrentContext.CancellationToken);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanRun, Is.EqualTo(installed.Count > 0));
            if (result.CanRun)
                return;

            Assert.That(result.Messages.Select(message => message.Code), Does.Contain("cursor.cli.missing"));
        });
    }

    [Test]
    public async Task CreateLaunchPlanAsync_usesAKnownAcpCandidateWhenInstalled()
    {
        var adapter = Adapter();
        var installed = await adapter.DiscoverAsync(TestContext.CurrentContext.CancellationToken);
        var validation = await adapter.ValidateAsync(Declaration(), installed, TestContext.CurrentContext.CancellationToken);
        if (!validation.CanRun)
        {
            Assert.That(installed, Is.Empty);
            return;
        }

        var plan = await adapter.CreateLaunchPlanAsync(Declaration(), installed, TestContext.CurrentContext.CancellationToken);
        AssertKnownLaunch(plan.Command, plan.Arguments);
    }

    private static CursorCapabilityAdapter Adapter() =>
        new(new CursorVersionProvider(new CapabilityCliLocator()));

    private static CapabilityDeclaration Declaration() =>
        new("workspace-agent", "cursor", new Dictionary<string, string>(), new Dictionary<string, string>());

    private static IReadOnlyList<string> WellKnownExecutables()
    {
        var probe = new CapabilityExecutableProbe();
        var names = CursorVersionProvider.Candidates.SelectMany(CandidateFileNames);
        return new CapabilitySearchPathProvider().Directories()
            .SelectMany(directory => names.Select(name => Path.Join(directory, name)))
            .Where(probe.IsExecutable)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> CandidateFileNames(CapabilityCliCandidate candidate)
    {
        if (OperatingSystem.IsWindows() && !candidate.FileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return [candidate.FileName, candidate.FileName + ".exe"];
        return [candidate.FileName];
    }

    private static void AssertKnownLaunch(string fileName, IReadOnlyList<string>? arguments)
    {
        var name = Path.GetFileName(fileName);
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];
        var candidate = CursorVersionProvider.Candidates
            .FirstOrDefault(item => item.FileName.Equals(name, StringComparison.OrdinalIgnoreCase));

        Assert.Multiple(() =>
        {
            Assert.That(candidate, Is.Not.Null, $"Launch command '{fileName}' is not a known Cursor ACP candidate.");
            Assert.That(arguments ?? [], Is.EqualTo(candidate!.LaunchArguments));
            if (Path.IsPathRooted(fileName))
                Assert.That(File.Exists(fileName), Is.True, $"Launch command '{fileName}' does not exist.");
        });
    }
}

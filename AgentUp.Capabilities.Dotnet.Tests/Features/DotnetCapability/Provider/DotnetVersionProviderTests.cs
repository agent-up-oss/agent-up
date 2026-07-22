using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;
using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Models;
using AgentUp.Capabilities.Common.Features.CapabilityInventory.Providers;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Providers;

namespace AgentUp.Capabilities.Dotnet.Tests.Features.DotnetCapability.Provider;

[TestFixture]
public sealed class DotnetVersionProviderTests
{
    private string? _previousInventoryPath;

    [SetUp]
    public void SetUp()
    {
        _previousInventoryPath = Environment.GetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable);
        Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString(), "missing.json"));
    }

    [TearDown]
    public void TearDown()
        => Environment.SetEnvironmentVariable(CapabilityInventoryFileProvider.InventoryPathVariable, _previousInventoryPath);

    [Test]
    public async Task DiscoverAsync_mergesCliAndUbuntuAptDiscovery()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("dotnet", "--list-sdks")] = new CapabilityCommandResult(0, "10.0.101 [/usr/share/dotnet/sdk]\n", "");
        commands.Results[("apt-cache", "policy dotnet-sdk-10.0")] = new CapabilityCommandResult(0, "Installed: 10.0.101-1\n", "");

        var versions = await new DotnetVersionProvider(new CapabilityInventoryFileProvider(), commands, "ubuntu").DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Select(version => version.Location), Does.Contain("/usr/share/dotnet/sdk"));
        Assert.That(versions.Select(version => version.Location), Does.Contain("apt:dotnet-sdk-10.0"));
        Assert.That(commands.Calls, Does.Contain(("apt-cache", "policy dotnet-sdk-10.0")));
    }

    [Test]
    public async Task DiscoverAsync_detectsHomebrewDotnetSdk()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("brew", "list --versions dotnet-sdk")] = new CapabilityCommandResult(0, "dotnet-sdk 10.0.101\n", "");

        var versions = await new DotnetVersionProvider(new CapabilityInventoryFileProvider(), commands, "macos").DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Single(version => version.Location == "brew:dotnet-sdk").Version, Is.EqualTo("10.0.101"));
    }

    [Test]
    public async Task DiscoverAsync_detectsChocolateyAndWingetDotnetSdk()
    {
        var commands = new RecordingCommandRunner();
        commands.Results[("choco", "list --local-only --exact dotnet-sdk")] = new CapabilityCommandResult(0, "dotnet-sdk 10.0.101\n", "");
        commands.Results[("winget", "list --id Microsoft.DotNet.SDK.10 --exact")] = new CapabilityCommandResult(0, "Microsoft .NET SDK 10 Microsoft.DotNet.SDK.10 10.0.101\n", "");

        var versions = await new DotnetVersionProvider(new CapabilityInventoryFileProvider(), commands, "windows").DiscoverAsync(CancellationToken.None);

        Assert.That(versions.Select(version => version.Location), Does.Contain("choco:dotnet-sdk"));
        Assert.That(versions.Select(version => version.Location), Does.Contain("winget:Microsoft.DotNet.SDK.10"));
    }

    private sealed class RecordingCommandRunner : ICapabilityCommandRunner
    {
        public Dictionary<(string FileName, string Arguments), CapabilityCommandResult> Results { get; } = [];
        public List<(string FileName, string Arguments)> Calls { get; } = [];

        public Task<CapabilityCommandResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
        {
            var key = (fileName, string.Join(" ", arguments));
            Calls.Add(key);
            return Task.FromResult(Results.GetValueOrDefault(key, new CapabilityCommandResult(127, "", "missing")));
        }
    }
}

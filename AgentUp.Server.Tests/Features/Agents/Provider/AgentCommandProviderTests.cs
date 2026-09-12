using AgentUp.Capabilities.Abstractions.Features.Capabilities.Interfaces;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentCommandProviderTests
{
    [Test]
    public async Task ResolveAsync_usesConfiguredAbsoluteExecutable()
    {
        var command = RootedShellCommand();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Agents:Codex:Command"] = command, ["Agents:Codex:Arguments:0"] = "serve"
        }).Build();

        var result = await new AgentCommandProvider(configuration, []).ResolveAsync(AgentKind.Codex, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result!.FileName, Is.EqualTo(command));
            Assert.That(result.Arguments, Is.EqualTo(new[] { "serve" }));
        });
    }

    [Test]
    public async Task IsAvailableAsync_requiresAnExistingExecutableForAbsoluteCommand()
    {
        var file = Path.GetTempFileName();
        try
        {
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Agents:Codex:Command"] = file }).Build();
            Assert.That(await new AgentCommandProvider(configuration, []).IsAvailableAsync(AgentKind.Codex, CancellationToken.None), Is.EqualTo(OperatingSystem.IsWindows()));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public async Task ResolveAsync_usesCapabilityLaunchPlanWhenCommandIsNotConfigured()
    {
        var adapter = new FakeAgentCapabilityAdapter("codex", "/home/dev/.local/bin/codex", ["acp"]);
        var provider = new AgentCommandProvider(new ConfigurationBuilder().Build(), [adapter]);

        var result = await provider.ResolveAsync(AgentKind.Codex, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result!.FileName, Is.EqualTo("/home/dev/.local/bin/codex"));
            Assert.That(result.Arguments, Is.EqualTo(new[] { "acp" }));
            Assert.That(adapter.DiscoverCalls, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ResolveAsync_prefersRootedConfigurationOverCapability()
    {
        var command = RootedShellCommand();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Agents:Cursor:Command"] = command
        }).Build();
        var adapter = new FakeAgentCapabilityAdapter("cursor", "/opt/agent", ["acp"]);

        var result = await new AgentCommandProvider(configuration, [adapter]).ResolveAsync(AgentKind.Cursor, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result!.FileName, Is.EqualTo(command));
            Assert.That(adapter.DiscoverCalls, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task IsAvailableAsync_isFalseWhenCapabilityFindsNothing()
    {
        var adapter = new FakeAgentCapabilityAdapter("claude");
        var available = await new AgentCommandProvider(new ConfigurationBuilder().Build(), [adapter])
            .IsAvailableAsync(AgentKind.Claude, CancellationToken.None);

        Assert.That(available, Is.False);
    }

    [Test]
    public async Task ResolveAsync_usesAPathCommandWhenItIsExecutable()
    {
        var command = OperatingSystem.IsWindows() ? "cmd" : "sh";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Agents:Codex:Command"] = command
        }).Build();

        var result = await new AgentCommandProvider(configuration, []).ResolveAsync(AgentKind.Codex, CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.FileName, Is.EqualTo(command));
    }

    [Test]
    public async Task ResolveAsync_isNullWhenCapabilityLaunchPlanHasNoCommand()
    {
        var adapter = new FakeAgentCapabilityAdapter("codex", "");
        var result = await new AgentCommandProvider(new ConfigurationBuilder().Build(), [adapter])
            .ResolveAsync(AgentKind.Codex, CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    private static string RootedShellCommand() =>
        OperatingSystem.IsWindows()
            ? Path.Join(Environment.SystemDirectory, "cmd.exe")
            : "/bin/sh";

    private sealed class FakeAgentCapabilityAdapter(string capabilityId, string? fileName = null, IReadOnlyList<string>? arguments = null) : ICapabilityAdapter
    {
        public CapabilityDescriptor Descriptor { get; } = new(capabilityId, capabilityId, "1.0.0", true, ["linux"]);
        public int DiscoverCalls { get; private set; }

        public Task<IReadOnlyList<CapabilityInstalledVersion>> DiscoverAsync(CancellationToken cancellationToken)
        {
            DiscoverCalls++;
            IReadOnlyList<CapabilityInstalledVersion> installed = fileName is null
                ? []
                : [new CapabilityInstalledVersion(capabilityId, "1.0.0", fileName, CapabilityVersionSource.System, false)];
            return Task.FromResult(installed);
        }

        public Task<CapabilityValidationResult> ValidateAsync(
            CapabilityDeclaration declaration,
            IReadOnlyList<CapabilityInstalledVersion> installedVersions,
            CancellationToken cancellationToken) =>
            Task.FromResult(installedVersions.Count == 0
                ? CapabilityValidationResult.Failure(new CapabilityValidationMessage($"{capabilityId}.cli.missing", "missing", CapabilityValidationSeverity.Error))
                : CapabilityValidationResult.Success());

        public Task<CapabilityLaunchPlan> CreateLaunchPlanAsync(
            CapabilityDeclaration declaration,
            IReadOnlyList<CapabilityInstalledVersion> installedVersions,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CapabilityLaunchPlan(fileName ?? "", Arguments: arguments ?? []));
    }
}

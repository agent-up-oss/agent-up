using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Tests.Support;
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

        var result = await new AgentCommandProvider(configuration).ResolveAsync("codex", CancellationToken.None);

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
            Assert.That(await new AgentCommandProvider(configuration).IsAvailableAsync("codex", CancellationToken.None), Is.EqualTo(OperatingSystem.IsWindows()));
        }
        finally { File.Delete(file); }
    }

    [Test]
    public async Task ResolveAsync_usesEnabledAgentPackageWhenCommandIsNotConfigured()
    {
        var packages = new FakeEnabledCapabilityPackages(FakeEnabledCapabilityPackages.Codex());
        var provider = new AgentCommandProvider(new ConfigurationBuilder().Build(), packages);

        var result = await provider.ResolveAsync("codex", CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result!.FileName, Is.EqualTo("codex-acp"));
            Assert.That(result.Arguments, Is.Empty);
        });
    }

    [Test]
    public async Task ResolveAsync_prefersRootedConfigurationOverCapability()
    {
        var command = RootedShellCommand();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Agents:Cursor:Command"] = command
        }).Build();
        var packages = new FakeEnabledCapabilityPackages(FakeEnabledCapabilityPackages.Cursor());

        var result = await new AgentCommandProvider(configuration, packages).ResolveAsync("cursor", CancellationToken.None);

        Assert.That(result!.FileName, Is.EqualTo(command));
    }

    [Test]
    public async Task IsAvailableAsync_isFalseWhenNoAgentPackageIsEnabled()
    {
        var available = await new AgentCommandProvider(new ConfigurationBuilder().Build())
            .IsAvailableAsync("claude", CancellationToken.None);

        Assert.That(available, Is.False);
    }

    [Test]
    public async Task ResolveAsync_usesAPathCommandWhenItIsExecutable()
    {
        var command = OperatingSystem.IsWindows() ? "cmd" : "sh";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Agents:Codex:Command"] = command
        }).Build();

        var result = await new AgentCommandProvider(configuration).ResolveAsync("codex", CancellationToken.None);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.FileName, Is.EqualTo(command));
    }

    [Test]
    public async Task ResolveAsync_isNullWhenEnabledAgentPackageHasNoCommand()
    {
        var packages = new FakeEnabledCapabilityPackages(new AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityPackageManifest
        {
            Id = "codex",
            Version = "1.0.0",
            DisplayName = "Codex",
            Kind = "agent"
        });
        var result = await new AgentCommandProvider(new ConfigurationBuilder().Build(), packages)
            .ResolveAsync("codex", CancellationToken.None);

        Assert.That(result, Is.Null);
    }

    private static string RootedShellCommand() =>
        OperatingSystem.IsWindows()
            ? Path.Join(Environment.SystemDirectory, "cmd.exe")
            : "/bin/sh";
}

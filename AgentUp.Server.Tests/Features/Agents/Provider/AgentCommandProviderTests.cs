using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;
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
    public Task IsAvailableAsync_isFalseWhenEnabledPackageCommandIsMissingFromPath()
        => WithEmptyPathAsync(async () =>
        {
            var packages = new FakeEnabledCapabilityPackages(FakeEnabledCapabilityPackages.Codex());
            var available = await new AgentCommandProvider(new ConfigurationBuilder().Build(), packages)
                .IsAvailableAsync("codex", CancellationToken.None);

            Assert.That(available, Is.False);
        });

    [Test]
    public async Task IsAvailableAsync_usesCapabilityLaunchWhenANonRootedConfiguredCommandIsMissing()
    {
        var command = OperatingSystem.IsWindows() ? "cmd" : "sh";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Agents:Codex:Command"] = "agent-up-missing-configured-cli"
        }).Build();
        var packages = new FakeEnabledCapabilityPackages(new AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityPackageManifest
        {
            Id = "codex",
            Version = "1.0.0",
            DisplayName = "Codex",
            Kind = "agent",
            Launch = new AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityLaunchTemplate
            {
                Command = command,
                Arguments = []
            }
        });
        var provider = new AgentCommandProvider(configuration, packages);

        Assert.That(await provider.IsAvailableAsync("codex", CancellationToken.None), Is.True);
        Assert.That((await provider.ResolveAsync("codex", CancellationToken.None))!.FileName, Is.EqualTo(command));
    }

    [Test]
    public async Task IsAvailableAsync_keepsAMissingRootedCommandAheadOfCapability()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Agents:Codex:Command"] = Path.Join(Path.GetTempPath(), "agent-up-missing-rooted-cli")
        }).Build();
        var packages = new FakeEnabledCapabilityPackages(new AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityPackageManifest
        {
            Id = "codex",
            Version = "1.0.0",
            DisplayName = "Codex",
            Kind = "agent",
            Launch = new AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityLaunchTemplate
            {
                Command = OperatingSystem.IsWindows() ? "cmd" : "sh",
                Arguments = []
            }
        });
        var provider = new AgentCommandProvider(configuration, packages);

        Assert.That(await provider.IsAvailableAsync("codex", CancellationToken.None), Is.False);
        Assert.That(await provider.ResolveAsync("codex", CancellationToken.None), Is.Null);
    }

    [Test]
    public async Task IsAvailableAsync_isTrueWhenEnabledPackageCommandExists()
    {
        var packages = new FakeEnabledCapabilityPackages(new AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityPackageManifest
        {
            Id = "codex",
            Version = "1.0.0",
            DisplayName = "Codex",
            Kind = "agent",
            Launch = new AgentUp.Capabilities.Abstractions.Features.Capabilities.Models.CapabilityLaunchTemplate
            {
                Command = OperatingSystem.IsWindows() ? "cmd" : "sh",
                Arguments = []
            }
        });
        var available = await new AgentCommandProvider(new ConfigurationBuilder().Build(), packages)
            .IsAvailableAsync("codex", CancellationToken.None);

        Assert.That(available, Is.True);
    }

    [Test]
    public async Task IsAvailableAsync_searchesTheCapabilityRuntimePath()
    {
        var directory = Directory.CreateTempSubdirectory();
        try
        {
            var command = OperatingSystem.IsWindows() ? "codex-acp.exe" : "codex-acp";
            var file = Path.Join(directory.FullName, command);
            await File.WriteAllBytesAsync(file, [0x23, 0x21]);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            var packages = new FakeEnabledCapabilityPackages(FakeEnabledCapabilityPackages.Codex());
            var provider = new AgentCommandProvider(
                new ConfigurationBuilder().Build(),
                packages,
                new StubRuntimePathProvider(directory.FullName));

            Assert.That(await provider.IsAvailableAsync("codex", CancellationToken.None), Is.True);
        }
        finally { directory.Delete(true); }
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

    [TestCase(AgentUnavailability.ScriptedTestAgent)]
    [TestCase(AgentUnavailability.LocalInferenceTestAgent)]
    public async Task IsAvailableAsync_findsTheDevelopmentTestAgentOnPath(string name)
    {
        var directory = Directory.CreateTempSubdirectory();
        var previous = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var file = Path.Join(directory.FullName, OperatingSystem.IsWindows() ? name + ".exe" : name);
            await File.WriteAllBytesAsync(file, [0x23, 0x21]);
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            Environment.SetEnvironmentVariable("PATH", directory.FullName + Path.PathSeparator + previous);
            var provider = new AgentCommandProvider(new ConfigurationBuilder().Build());

            Assert.That(await provider.IsAvailableAsync(name, CancellationToken.None), Is.True);
            Assert.That((await provider.ResolveAsync(name, CancellationToken.None))!.FileName, Is.EqualTo(name));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previous);
            directory.Delete(true);
        }
    }

    [Test]
    public void LaunchCommandName_returnsTheConfiguredFileName()
    {
        var command = RootedShellCommand();
        var provider = new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Agents:Codex:Command"] = command }).Build());

        Assert.That(provider.LaunchCommandName("codex"), Is.EqualTo(Path.GetFileName(command)));
    }

    [Test]
    public void LaunchCommandName_returnsTheUnwrappedPackageCommand()
    {
        var packages = new FakeEnabledCapabilityPackages(FakeEnabledCapabilityPackages.Claude());
        var provider = new AgentCommandProvider(new ConfigurationBuilder().Build(), packages);

        Assert.That(provider.LaunchCommandName("claude"), Is.EqualTo("claude-agent-acp"));
    }

    [Test]
    public void LaunchCommandName_returnsTheAgentLaunchFileName()
    {
        var packages = new FakeEnabledCapabilityPackages(FakeEnabledCapabilityPackages.Codex())
            .WithAgents(new StubAgentCapability
            {
                LaunchResult = new AgentUp.Sdk.Agent.AgentLaunchResult("codex-cli", [], null)
            });
        var provider = new AgentCommandProvider(new ConfigurationBuilder().Build(), packages);

        Assert.That(provider.LaunchCommandName("codex"), Is.EqualTo("codex-cli"));
    }

    [Test]
    public void LaunchCommandName_returnsTheScriptedTestAgentWhenNoPackageIsEnabled()
    {
        var provider = new AgentCommandProvider(new ConfigurationBuilder().Build());

        Assert.That(provider.LaunchCommandName(AgentUnavailability.ScriptedTestAgent), Is.EqualTo(AgentUnavailability.ScriptedTestAgent));
        Assert.That(provider.LaunchCommandName(AgentUnavailability.LocalInferenceTestAgent), Is.EqualTo(AgentUnavailability.LocalInferenceTestAgent));
        Assert.That(provider.LaunchCommandName("claude"), Is.Null);
    }

    [TestCase(AgentUnavailability.ScriptedTestAgent)]
    [TestCase(AgentUnavailability.LocalInferenceTestAgent)]
    public Task ResolveAsync_isNullWhenTheDevelopmentTestAgentIsMissingFromPath(string name)
        => WithEmptyPathAsync(async () =>
        {
            var result = await new AgentCommandProvider(new ConfigurationBuilder().Build())
                .ResolveAsync(name, CancellationToken.None);

            Assert.That(result, Is.Null);
        });

    [Test]
    public async Task IsAvailableAsync_usesTheAgentLaunchFileName()
    {
        var packages = new FakeEnabledCapabilityPackages(FakeEnabledCapabilityPackages.Codex())
            .WithAgents(new StubAgentCapability
            {
                LaunchResult = new AgentUp.Sdk.Agent.AgentLaunchResult(
                    OperatingSystem.IsWindows() ? "cmd" : "sh",
                    [],
                    null)
            });
        var available = await new AgentCommandProvider(new ConfigurationBuilder().Build(), packages)
            .IsAvailableAsync("codex", CancellationToken.None);

        Assert.That(available, Is.True);
    }

    [Test]
    public void LaunchCommandName_fallsBackToTheEnabledPackageWhenLaunchFileNameIsBlank()
    {
        var packages = new FakeEnabledCapabilityPackages(FakeEnabledCapabilityPackages.Claude())
            .WithAgents(new StubAgentCapability
            {
                Identity = new("claude", "1.0.0", "Claude", "agent-up"),
                LaunchResult = new AgentUp.Sdk.Agent.AgentLaunchResult("  ", [], null)
            });
        var provider = new AgentCommandProvider(new ConfigurationBuilder().Build(), packages);

        Assert.That(provider.LaunchCommandName("claude"), Is.EqualTo("claude-agent-acp"));
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

    private static async Task WithEmptyPathAsync(Func<Task> body)
    {
        var directory = Directory.CreateTempSubdirectory();
        var previous = Environment.GetEnvironmentVariable("PATH");
        Environment.SetEnvironmentVariable("PATH", directory.FullName);
        try
        {
            await body();
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previous);
            directory.Delete(true);
        }
    }

    private sealed class StubRuntimePathProvider(string path) : IAgentRuntimePathProvider
    {
        public IReadOnlyList<string> PathPrefixes() => [path];

        public string? RuntimeRoot() => null;
    }
}

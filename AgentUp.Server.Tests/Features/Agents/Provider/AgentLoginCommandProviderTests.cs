using AgentUp.Sdk.Agent;
using AgentUp.Sdk.Common;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentLoginCommandProviderTests
{
    [Test]
    public void Resolve_usesTheCursorAcpBinaryForNoBrowserLogin()
    {
        var provider = new AgentLoginCommandProvider(new ConfigurationBuilder().Build(), new AgentSubscriptionAuth());
        var command = provider.Resolve("cursor", new AgentCommand("/opt/agent-up/bin/agent", ["acp"]), "cursor_login");

        Assert.Multiple(() =>
        {
            Assert.That(command.FileName, Is.EqualTo("/opt/agent-up/bin/agent"));
            Assert.That(command.Arguments, Is.EqualTo(new[] { "login" }));
            Assert.That(command.Environment["NO_OPEN_BROWSER"], Is.EqualTo("1"));
        });
    }

    [Test]
    public void Resolve_rejectsApiKeyMethods()
    {
        var provider = new AgentLoginCommandProvider(new ConfigurationBuilder().Build(), new AgentSubscriptionAuth());

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.Resolve("codex", new AgentCommand("/opt/codex-acp", []), "api-key"));

        Assert.That(exception!.Message, Does.Contain("subscription"));
    }

    [Test]
    public void Resolve_usesAConfiguredLoginCommandAndACodexSibling()
    {
        var directory = Directory.CreateTempSubdirectory("agent-login-command");
        try
        {
            var sibling = Path.Join(directory.FullName, "codex");
            File.WriteAllText(sibling, "#!/bin/sh\n");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(sibling, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var configured = new AgentLoginCommandProvider(
                new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Agents:Claude:LoginCommand"] = sibling,
                    ["Agents:Claude:LoginArguments:0"] = "setup-token"
                }).Build(),
                new AgentSubscriptionAuth());

            var claude = configured.Resolve("claude", new AgentCommand("/missing/claude-agent-acp", []), "claude-login");
            var codex = new AgentLoginCommandProvider(new ConfigurationBuilder().Build(), new AgentSubscriptionAuth())
                .Resolve("codex", new AgentCommand(Path.Join(directory.FullName, "codex-acp"), []), "chatgpt");

            Assert.Multiple(() =>
            {
                Assert.That(claude.FileName, Is.EqualTo(sibling));
                Assert.That(claude.Arguments, Is.EqualTo(new[] { "setup-token" }));
                Assert.That(codex.FileName, Is.EqualTo(sibling));
                Assert.That(codex.Arguments, Is.EqualTo(new[] { "login", "--device-auth" }));
            });
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Test]
    public void Resolve_usesAClaudeSiblingAndFallsBackToPath()
    {
        var directory = Directory.CreateTempSubdirectory("agent-login-claude");
        var pathDirectory = Directory.CreateTempSubdirectory("agent-login-path");
        var previousPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            var claude = Path.Join(directory.FullName, "claude");
            File.WriteAllText(claude, "#!/bin/sh\n");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(claude, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var pathCodex = Path.Join(pathDirectory.FullName, OperatingSystem.IsWindows() ? "codex.CMD" : "codex");
            File.WriteAllText(pathCodex, OperatingSystem.IsWindows() ? "@echo off\r\n" : "#!/bin/sh\n");
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(pathCodex, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Environment.SetEnvironmentVariable("PATH", pathDirectory.FullName);

            var provider = new AgentLoginCommandProvider(new ConfigurationBuilder().Build(), new AgentSubscriptionAuth());
            var fromSibling = provider.Resolve(
                "claude", new AgentCommand(Path.Join(directory.FullName, "claude-agent-acp"), []), "claude-login");
            var fromPath = provider.Resolve(
                "codex", new AgentCommand(Path.Join(directory.FullName, "codex-acp"), []), "chatgpt");

            Assert.Multiple(() =>
            {
                Assert.That(fromSibling.FileName, Is.EqualTo(claude));
                Assert.That(fromSibling.Arguments, Is.EqualTo(new[] { "setup-token" }));
                Assert.That(fromPath.FileName, Is.EqualTo("codex"));
            });
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            directory.Delete(true);
            pathDirectory.Delete(true);
        }
    }

    [Test]
    public void Resolve_usesTheModuleLoginSpecWhenPresent()
    {
        var packages = new FakeEnabledCapabilityPackages().WithAgents(new StubAgentCapability
        {
            Identity = new CapabilityIdentity("sample", "1.0.0", "Sample", "agent-up"),
            Login = new AgentLoginSpec("sample", ["auth"], "poll")
        });
        var provider = new AgentLoginCommandProvider(new ConfigurationBuilder().Build(), new AgentSubscriptionAuth(), packages);

        var command = provider.Resolve("sample", new AgentCommand("/opt/agent-up/bin/sample", ["acp"]), "sample-login");

        Assert.Multiple(() =>
        {
            Assert.That(command.FileName, Is.EqualTo("/opt/agent-up/bin/sample"));
            Assert.That(command.Arguments, Is.EqualTo(new[] { "auth" }));
        });
    }

    [Test]
    public void Resolve_fallsBackToFirstPartyModuleIdsWhenLoginSpecIsMissing()
    {
        var packages = new FakeEnabledCapabilityPackages().WithAgents(new StubAgentCapability
        {
            Identity = new CapabilityIdentity("cursor", "1.0.0", "Cursor", "agent-up")
        });
        var provider = new AgentLoginCommandProvider(new ConfigurationBuilder().Build(), new AgentSubscriptionAuth(), packages);

        var command = provider.Resolve("cursor", new AgentCommand("/opt/agent-up/bin/agent", ["acp"]), "cursor_login");

        Assert.That(command.Arguments, Is.EqualTo(new[] { "login" }));
    }

    [Test]
    public void Resolve_rejectsUnknownModulesWithoutALoginSpec()
    {
        var packages = new FakeEnabledCapabilityPackages().WithAgents(new StubAgentCapability
        {
            Identity = new CapabilityIdentity("other", "1.0.0", "Other", "agent-up")
        });
        var provider = new AgentLoginCommandProvider(new ConfigurationBuilder().Build(), new AgentSubscriptionAuth(), packages);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            provider.Resolve("other", new AgentCommand("/opt/other-acp", []), "other-login"));

        Assert.That(exception!.Message, Does.Contain("not supported"));
    }

    [Test]
    public void Resolve_failsWhenTheLoginCliIsMissing()
    {
        var directory = Directory.CreateTempSubdirectory("agent-login-missing");
        var previousPath = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", directory.FullName);
            var provider = new AgentLoginCommandProvider(new ConfigurationBuilder().Build(), new AgentSubscriptionAuth());

            var exception = Assert.Throws<InvalidOperationException>(() =>
                provider.Resolve("codex", new AgentCommand(Path.Join(directory.FullName, "codex-acp"), []), "chatgpt"));

            Assert.That(exception!.Message, Does.Contain("codex is required"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previousPath);
            directory.Delete(true);
        }
    }
}

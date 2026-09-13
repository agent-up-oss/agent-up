using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.Extensions.Configuration;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentLoginCommandProviderTests
{
    [Test]
    public void Resolve_usesTheCursorAcpBinaryForNoBrowserLogin()
    {
        var provider = new AgentLoginCommandProvider(new ConfigurationBuilder().Build(), new AgentSubscriptionAuth());
        var command = provider.Resolve(AgentKind.Cursor, new AgentCommand("/opt/agent-up/bin/agent", ["acp"]), "cursor_login");

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
            provider.Resolve(AgentKind.Codex, new AgentCommand("/opt/codex-acp", []), "api-key"));

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

            var claude = configured.Resolve(AgentKind.Claude, new AgentCommand("/missing/claude-agent-acp", []), "claude-login");
            var codex = new AgentLoginCommandProvider(new ConfigurationBuilder().Build(), new AgentSubscriptionAuth())
                .Resolve(AgentKind.Codex, new AgentCommand(Path.Join(directory.FullName, "codex-acp"), []), "chatgpt");

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
}

using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Providers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AcpProcessProviderTests
{
    [Test]
    public async Task CallAsync_writesJsonRpcAndMatchesResponseById()
    {
        var command = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var arguments = OperatingSystem.IsWindows()
            ? new[] { "/d", "/s", "/c", "set /p line=& echo {\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":1}}" }
            : new[] { "-c", "read line; printf '%s\\n' '{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":{\"protocolVersion\":1}}'" };
        var values = new Dictionary<string, string?> { ["Agents:Codex:Command"] = command };
        for (var index = 0; index < arguments.Length; index++) values[$"Agents:Codex:Arguments:{index}"] = arguments[index];
        var commands = new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        await using var provider = new AcpProcessProvider(commands, NullLogger<AcpProcessProvider>.Instance);
        await provider.StartAsync(AgentKind.Codex, Path.GetTempPath(), CancellationToken.None);

        var result = await provider.CallAsync("initialize", new { protocolVersion = 1 }, CancellationToken.None);

        Assert.That(result.GetProperty("protocolVersion").GetInt32(), Is.EqualTo(1));
    }
}

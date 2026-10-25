using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Resolves the sign-in flow for an agent module. Defaults come from that module's
/// <c>Login</c> spec when present; <c>Agents:{agent}:LoginTransport</c> overrides
/// them when a deployment points an agent at a different CLI or a different login subcommand.
/// </summary>
public sealed class AgentLoginFlowProvider(IConfiguration configuration, IEnabledCapabilityPackages? packages = null)
{
    public AgentLoginFlow Resolve(string agent)
    {
        var configured = configuration[$"Agents:{agent}:LoginTransport"];
        var flow = string.IsNullOrWhiteSpace(configured) ? Default(agent) : FromDeclaration(agent, configured);
        return flow with
        {
            ChallengeTimeout = ReadTimeout($"Agents:{agent}:LoginChallengeTimeoutSeconds") ?? flow.ChallengeTimeout,
            CompletionTimeout = ReadTimeout($"Agents:{agent}:LoginCompletionTimeoutSeconds") ?? flow.CompletionTimeout
        };
    }

    internal AgentLoginFlow Default(string agent)
    {
        var transport = packages?.GetAgent(agent)?.Login?.Transport;
        if (!string.IsNullOrWhiteSpace(transport))
        {
            // Codex 1.0.0 called its device flow "code", the same legacy spelling Claude uses
            // for a code pasted back into stdin. Preserve installed 1.0.0 packages while making
            // the direction unambiguous: Codex's --device-auth always prints the code for the
            // user to enter on the website.
            return FromDeclaration(agent, transport);
        }

        // First-party module ids only, used when an enabled module has no Login spec.
        return agent.ToLowerInvariant() switch
        {
            "codex" => AgentLoginFlow.DeviceCode(),
            "cursor" => AgentLoginFlow.Poll(),
            "claude" => AgentLoginFlow.PastedCode(),
            _ => AgentLoginFlow.Poll()
        };
    }

    private static AgentLoginFlow FromDeclaration(string agent, string transport)
    {
        if (agent.Equals("codex", StringComparison.OrdinalIgnoreCase)
            && transport.Trim().Equals("code", StringComparison.OrdinalIgnoreCase))
            return AgentLoginFlow.DeviceCode();
        return FromName(transport);
    }

    private TimeSpan? ReadTimeout(string key)
    {
        var value = configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            return null;
        if (!double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out var seconds) || seconds <= 0)
            throw new InvalidOperationException($"'{key}' must be a positive number of seconds.");
        return TimeSpan.FromSeconds(seconds);
    }

    internal static AgentLoginFlow FromName(string name) => name.Trim().ToLowerInvariant() switch
    {
        "poll" => AgentLoginFlow.Poll(),
        "device" or "devicecode" or "device-code" => AgentLoginFlow.DeviceCode(),
        "paste" or "pastedcode" or "pasted-code" or "code" => AgentLoginFlow.PastedCode(),
        "redirect" or "loopback" => AgentLoginFlow.Redirect(),
        _ => throw new InvalidOperationException(
            $"'{name}' is not a supported login transport. Use poll, device, paste, or redirect.")
    };
}

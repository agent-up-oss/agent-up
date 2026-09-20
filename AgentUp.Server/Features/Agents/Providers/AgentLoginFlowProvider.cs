using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Resolves the sign-in flow for an agent kind. Defaults match the login commands
/// <see cref="AgentLoginCommandProvider"/> runs; <c>Agents:{kind}:LoginTransport</c> overrides
/// them when a deployment points a kind at a different CLI or a different login subcommand.
/// </summary>
public sealed class AgentLoginFlowProvider(IConfiguration configuration)
{
    public AgentLoginFlow Resolve(AgentKind kind)
    {
        var configured = configuration[$"Agents:{kind}:LoginTransport"];
        var flow = string.IsNullOrWhiteSpace(configured) ? Default(kind) : FromName(configured);
        return flow with
        {
            ChallengeTimeout = ReadTimeout($"Agents:{kind}:LoginChallengeTimeoutSeconds") ?? flow.ChallengeTimeout,
            CompletionTimeout = ReadTimeout($"Agents:{kind}:LoginCompletionTimeoutSeconds") ?? flow.CompletionTimeout
        };
    }

    internal static AgentLoginFlow Default(AgentKind kind) => kind switch
    {
        // codex login --device-auth: prints a link and a user code, then polls.
        AgentKind.Codex => AgentLoginFlow.DeviceCode(),
        // cursor login: prints a link tied to a login id, then polls on its own.
        AgentKind.Cursor => AgentLoginFlow.Poll(),
        // claude setup-token: prints a link, then blocks on stdin for the pasted code.
        AgentKind.Claude => AgentLoginFlow.PastedCode(),
        _ => AgentLoginFlow.Poll()
    };

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

using System.Text;

namespace AgentUp.Server.Features.Authentication.Providers;

public static class WebSocketAuthenticationProtocol
{
    public const string Prefix = "agent-up.auth.";

    /// <summary>Reads a bearer credential from the Agent-Up WebSocket subprotocol.</summary>
    public static string? ReadToken(bool isWebSocketRequest, string requestedProtocols)
    {
        if (!isWebSocketRequest) return null;

        var protocol = requestedProtocols
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(value => value.StartsWith(Prefix, StringComparison.Ordinal));
        if (protocol is null) return null;

        try
        {
            var encoded = protocol[Prefix.Length..].Replace('-', '+').Replace('_', '/');
            encoded = encoded.PadRight(encoded.Length + ((4 - encoded.Length % 4) % 4), '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}

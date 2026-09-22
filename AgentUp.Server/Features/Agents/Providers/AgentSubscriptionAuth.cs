using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentSubscriptionAuth
{
    public IReadOnlyList<AgentAuthMethodDto> KeepSubscription(IReadOnlyList<AgentAuthMethodDto> methods) =>
        methods.Where(method => !IsApiKeyMethod(method.Id, method.Name)).ToArray();

    public IReadOnlyList<AgentAuthMethodDto> Defaults(string agent) => agent.ToLowerInvariant() switch
    {
        "codex" =>
        [
            new AgentAuthMethodDto(
                "chatgpt",
                "ChatGPT",
                "Open the sign-in link, then enter the code in ChatGPT. This uses your ChatGPT subscription.")
        ],
        "cursor" =>
        [
            new AgentAuthMethodDto(
                "cursor_login",
                "Cursor Login",
                "Open the sign-in link and sign in with your Cursor subscription.")
        ],
        "claude" =>
        [
            new AgentAuthMethodDto(
                "claude-login",
                "Claude Pro",
                "Open the sign-in link and sign in with your Claude subscription.")
        ],
        _ => []
    };

    public bool IsApiKeyMethod(string id, string? name)
    {
        if (id.Equals("api-key", StringComparison.OrdinalIgnoreCase)
            || id.Equals("apikey", StringComparison.OrdinalIgnoreCase)
            || id.Equals("api_key", StringComparison.OrdinalIgnoreCase)
            || id.EndsWith("-api-key", StringComparison.OrdinalIgnoreCase)
            || id.EndsWith("_api_key", StringComparison.OrdinalIgnoreCase))
            return true;
        return name is not null && name.Contains("API Key", StringComparison.OrdinalIgnoreCase);
    }

    public bool LooksLikeAuthenticationFailure(string message) =>
        message.Contains("auth", StringComparison.OrdinalIgnoreCase)
        || message.Contains("sign in", StringComparison.OrdinalIgnoreCase)
        || message.Contains("login", StringComparison.OrdinalIgnoreCase);
}

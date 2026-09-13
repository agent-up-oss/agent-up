using System.Text.RegularExpressions;
using AgentUp.Server.Features.Agents.DTOs;

namespace AgentUp.Server.Features.Agents.Providers;

public sealed class AgentSubscriptionLoginParser
{
    private static readonly Regex HttpUrl = new(@"https://[^\s""'<>\\]+", RegexOptions.CultureInvariant);
    private static readonly Regex DeviceCode = new(@"\b[A-Z0-9]{4,5}-[A-Z0-9]{4,8}\b", RegexOptions.CultureInvariant);
    private static readonly Regex ClaudeToken = new(@"sk-ant-oat[A-Za-z0-9_\-]+", RegexOptions.CultureInvariant);

    private string? _url;
    private string? _code;
    private string? _token;
    private bool _mentionsCode;

    public AgentLoginChallengeDto Challenge =>
        new(_url, _code, Instructions(_url, _code));

    public string? ClaudeOAuthToken => _token;

    public bool Append(string line)
    {
        var changed = false;
        if (line.Contains("code", StringComparison.OrdinalIgnoreCase))
            _mentionsCode = true;

        var url = ChooseUrl(line);
        if (url is not null && !string.Equals(_url, url, StringComparison.Ordinal))
        {
            _url = url;
            changed = true;
        }

        if (_mentionsCode && _code is null)
        {
            var code = MatchDeviceCode(line);
            if (code is not null)
            {
                _code = code;
                changed = true;
            }
        }

        var token = ClaudeToken.Match(line);
        if (token.Success && !token.Value.StartsWith("sk-ant-api", StringComparison.Ordinal))
            _token = token.Value;

        return changed;
    }

    internal static string? ChooseUrl(string line)
    {
        string? fallback = null;
        foreach (Match match in HttpUrl.Matches(line))
        {
            var url = match.Value.TrimEnd('.', ',', ';', ')', ']');
            if (IsPreferredLoginUrl(url))
                return url;
            fallback ??= url;
        }

        return fallback;
    }

    internal static bool IsPreferredLoginUrl(string url) =>
        url.Contains("loginDeepControl", StringComparison.OrdinalIgnoreCase)
        || url.Contains("cursor.com/login", StringComparison.OrdinalIgnoreCase)
        || url.Contains("auth.openai.com", StringComparison.OrdinalIgnoreCase)
        || url.Contains("chatgpt.com", StringComparison.OrdinalIgnoreCase)
        || url.Contains("claude.ai", StringComparison.OrdinalIgnoreCase)
        || url.Contains("anthropic.com", StringComparison.OrdinalIgnoreCase);

    internal static string Instructions(string? url, string? code)
    {
        if (!string.IsNullOrWhiteSpace(code))
            return "Open this link, then enter the code. This uses your subscription, not an API key.";
        if (!string.IsNullOrWhiteSpace(url))
            return "Open this link and sign in with your subscription.";
        return "Waiting for the agent CLI to print a sign-in link.";
    }

    private static string? MatchDeviceCode(string line)
    {
        var match = DeviceCode.Match(line);
        return match.Success ? match.Value : null;
    }
}

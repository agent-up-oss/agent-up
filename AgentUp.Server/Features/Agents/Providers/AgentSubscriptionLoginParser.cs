using System.Text.RegularExpressions;
using AgentUp.Server.Features.Agents.DTOs;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Reads the sign-in link, user code, and expiry out of an agent CLI's terminal output.
/// <para>
/// The transport is not inferred here. <see cref="AgentLoginFlowProvider"/> already declared it,
/// so this only has to find the values the declared flow needs, and a flow that wants no user
/// code never adopts a stray code-shaped token from a progress message.
/// </para>
/// </summary>
public sealed class AgentSubscriptionLoginParser(AgentLoginFlow flow)
{
    private static readonly Regex Ansi = new(@"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", RegexOptions.CultureInvariant);
    private static readonly Regex HttpUrl = new(@"https?://[^\s""'<>\\]+", RegexOptions.CultureInvariant);
    private static readonly Regex DeviceCode = new(@"\b[A-Z0-9]{4,5}-[A-Z0-9]{4,8}\b", RegexOptions.CultureInvariant);
    private static readonly Regex ClaudeToken = new(@"sk-ant-oat[A-Za-z0-9_\-]+", RegexOptions.CultureInvariant);
    private static readonly Regex ExpiresIn = new(@"expires?\s+in\s+(\d+)\s*(second|minute)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private string? _url;
    private string? _code;
    private string? _token;
    private string? _redirectUri;
    private DateTimeOffset? _expiresAt;
    private bool _mentionsCode;
    private bool _awaitingCodeInput;
    private bool _urlStartsTheSignIn;

    public AgentLoginChallengeDto Challenge =>
        new(_url,
            _code,
            Instructions(flow, _url, _code),
            flow.Transport,
            flow.NeedsCodeInput && _awaitingCodeInput,
            _expiresAt,
            _redirectUri);

    public string? ClaudeOAuthToken => _token;

    /// <summary>True once the CLI has written a prompt it is waiting on stdin to answer.</summary>
    public bool AwaitingCodeInput => _awaitingCodeInput;

    public bool Append(string segment)
    {
        var line = Ansi.Replace(segment, string.Empty);
        var changed = false;
        if (line.Contains("code", StringComparison.OrdinalIgnoreCase))
            _mentionsCode = true;

        var url = ChooseUrl(line);
        if (url is not null && Read(url))
            changed = true;

        if (flow.Transport == AgentLoginTransport.Code && !flow.NeedsCodeInput && _mentionsCode && _code is null)
        {
            var code = DeviceCode.Match(line);
            if (code.Success)
            {
                _code = code.Value;
                changed = true;
            }
        }

        if (flow.NeedsCodeInput && !_awaitingCodeInput && IsCodePrompt(line))
        {
            _awaitingCodeInput = true;
            changed = true;
        }

        if (_expiresAt is null && ReadExpiry(line) is { } expiry)
        {
            _expiresAt = expiry;
            changed = true;
        }

        var token = ClaudeToken.Match(line);
        if (token.Success && !token.Value.StartsWith("sk-ant-api", StringComparison.Ordinal))
            _token = token.Value;

        return changed;
    }

    /// <summary>
    /// A prompt the CLI is blocking on. These arrive without a trailing newline, which is why
    /// <see cref="AgentLoginOutputReader"/> flushes on idle rather than on line breaks alone.
    /// </summary>
    internal static bool IsCodePrompt(string line)
    {
        var trimmed = line.TrimEnd();
        if (trimmed.Length == 0)
            return false;
        if (!trimmed.EndsWith(':') && !trimmed.EndsWith('?') && !trimmed.EndsWith('>'))
            return false;
        return trimmed.Contains("code", StringComparison.OrdinalIgnoreCase)
            || trimmed.Contains("paste", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Takes what a URL tells us, and reports whether anything changed.
    /// </summary>
    /// <remarks>
    /// A CLI prints more than one. codex announces the loopback address it is listening on as
    /// well as the link that starts the sign-in, and it does not always print them in that order.
    /// Letting the last one win handed the user the callback address - a page nothing serves
    /// until the sign-in is already finished - and lost the redirect the client has to watch for,
    /// so once the link that starts the sign-in has been seen, nothing displaces it.
    /// </remarks>
    private bool Read(string url)
    {
        if (string.Equals(_url, url, StringComparison.Ordinal))
            return false;

        var startsTheSignIn = IsPreferredLoginUrl(url);
        if (!_urlStartsTheSignIn || startsTheSignIn)
        {
            var displaced = _url;
            _url = url;
            _urlStartsTheSignIn = startsTheSignIn;
            // Keep a callback address rather than lose it: the one this URL carries, else the
            // loopback address this URL just displaced, else whatever was already known.
            _redirectUri = ReadRedirectUri(url) ?? Loopback(displaced) ?? _redirectUri;
            return true;
        }

        // "Waiting for the sign-in to come back to http://localhost:1455/callback": a CLI that
        // keeps its callback address out of the authorization query still says it out loud, and
        // the client cannot recognise the redirect it must carry back without it.
        if (_redirectUri is null && IsLoopback(url))
        {
            _redirectUri = url;
            return true;
        }

        return false;
    }

    private static string? Loopback(string? url) => url is not null && IsLoopback(url) ? url : null;

    private static bool IsLoopback(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) && parsed.IsLoopback;

    internal static string? ChooseUrl(string line)
    {
        var urls = HttpUrl.Matches(line)
            .Select(match => match.Value.TrimEnd('.', ',', ';', ')', ']'))
            .ToArray();
        return urls.FirstOrDefault(IsPreferredLoginUrl) ?? urls.FirstOrDefault();
    }

    internal static bool IsPreferredLoginUrl(string url) =>
        url.Contains("loginDeepControl", StringComparison.OrdinalIgnoreCase)
        || url.Contains("cursor.com/login", StringComparison.OrdinalIgnoreCase)
        || url.Contains("auth.openai.com", StringComparison.OrdinalIgnoreCase)
        || url.Contains("chatgpt.com", StringComparison.OrdinalIgnoreCase)
        || url.Contains("claude.ai", StringComparison.OrdinalIgnoreCase)
        || url.Contains("anthropic.com", StringComparison.OrdinalIgnoreCase)
        || url.Contains("/authorize", StringComparison.OrdinalIgnoreCase)
        || url.Contains("/device", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The loopback address the CLI is listening on, lifted out of the authorization URL. The
    /// client needs it to recognise the redirect it must intercept and hand back.
    /// </summary>
    internal static string? ReadRedirectUri(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed))
            return null;
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(parsed.Query);
        return query.TryGetValue("redirect_uri", out var redirect) && !string.IsNullOrWhiteSpace(redirect)
            ? redirect.ToString()
            : null;
    }

    internal static DateTimeOffset? ReadExpiry(string line)
    {
        var match = ExpiresIn.Match(line);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var amount))
            return null;
        var unit = match.Groups[2].Value;
        var span = unit.StartsWith("minute", StringComparison.OrdinalIgnoreCase)
            ? TimeSpan.FromMinutes(amount)
            : TimeSpan.FromSeconds(amount);
        return DateTimeOffset.UtcNow + span;
    }

    internal static string Instructions(AgentLoginFlow flow, string? url, string? code)
    {
        if (url is null)
            return "Waiting for the agent CLI to print a sign-in link.";
        return flow.Transport switch
        {
            AgentLoginTransport.Code when flow.NeedsCodeInput =>
                "Open this link, sign in, then paste the code it gives you back here. This uses your subscription, not an API key.",
            AgentLoginTransport.Code when code is not null =>
                "Open this link, then enter the code shown here. This uses your subscription, not an API key.",
            AgentLoginTransport.Code =>
                "Open this link and enter the code it asks for. This uses your subscription, not an API key.",
            AgentLoginTransport.Redirect =>
                "Open this link and sign in. Agent-Up will pick the sign-in up automatically when it finishes.",
            _ => "Open this link and sign in with your subscription."
        };
    }
}

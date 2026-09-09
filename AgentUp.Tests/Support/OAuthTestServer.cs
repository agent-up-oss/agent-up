using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AgentUp.Tests.Support;

// A complete, if small, OAuth 2.0 authorization-code-with-PKCE deployment on loopback: it plays
// both the relying application a workspace runs and the identity provider that application signs
// in against. Desktop end-to-end tests drive it through the real embedded WebView, so the
// authorization redirect, the browser-held session cookie, and the back-channel token exchange
// all happen over real HTTP exactly as they do for Google, GitHub, Microsoft, or Auth0.
//
// Routes:
//   GET  /                    the application page (server-rendered sign-in state)
//   GET  /?signout=1          clears the browser session, then renders the page
//   GET  /signin              application: mints state + PKCE verifier, redirects to authorize
//   GET  /oauth/authorize     provider: validates the request and redirects back with a code
//   GET  /oauth/callback      application: exchanges the code, then sets the session cookie
//   POST /oauth/token         provider: verifies PKCE and issues an access token
//   GET  /session             application: the signed-in user as JSON, for page polling
internal sealed class OAuthTestServer : IDisposable
{
    internal const string ClientId = "agent-up-desktop-e2e";
    internal const string SignedInUser = "alice";
    internal const string SessionCookieName = "agentup_session";

    private const string PageTemplate = """
        <!DOCTYPE html>
        <html><body>
          <h1 id="app">Workspace application</h1>
          <p id="status">__AGENTUP_STATUS__</p>
          <script>
            window.__nav = '__AGENTUP_NAV__';
            window.__signIn = function () { window.location.href = '/signin'; return 'navigating'; };
            function refresh() {
              fetch('/session', { cache: 'no-store' })
                .then(function (response) { return response.json(); })
                .then(function (session) {
                  document.getElementById('status').textContent =
                    session.user ? ('signed-in:' + session.user) : 'signed-out';
                });
            }
            setInterval(refresh, 250);
            refresh();
          </script>
        </body></html>
        """;

    private readonly HttpListener _listener;
    private readonly HttpClient _backChannel = new();
    private readonly ConcurrentDictionary<string, string> _pendingVerifiers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _issuedCodes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _sessions = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<IReadOnlyDictionary<string, string>> _authorizeRequests = new();
    private readonly SemaphoreSlim _authorizeSignal = new(0);
    private int _tokenGrants;
    private int _rejectedRequests;

    internal int Port { get; }

    internal string BaseUrl => $"http://localhost:{Port}/";

    // Authorization codes exchanged for an access token with a valid PKCE verifier.
    internal int TokenGrants => Volatile.Read(ref _tokenGrants);

    // Authorize, callback, or token requests the deployment refused, for example on a PKCE
    // mismatch or an unknown state value.
    internal int RejectedRequests => Volatile.Read(ref _rejectedRequests);

    internal OAuthTestServer()
    {
        Port = LoopbackPorts.FindFree();
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        _listener.Prefixes.Add($"http://localhost:{Port}/");
        _listener.Start();
        _backChannel.BaseAddress = new Uri(BaseUrl);
        _ = AcceptAsync();
    }

    internal void Reset()
    {
        _pendingVerifiers.Clear();
        _issuedCodes.Clear();
        _sessions.Clear();
        _authorizeRequests.Clear();
        Volatile.Write(ref _tokenGrants, 0);
        Volatile.Write(ref _rejectedRequests, 0);
    }

    internal async Task<IReadOnlyDictionary<string, string>> WaitForAuthorizeRequestAsync(TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (_authorizeRequests.TryDequeue(out var request))
                return request;

            await _authorizeSignal.WaitAsync(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException("The identity provider never received an authorization request.");
    }

    private async Task AcceptAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            // Each request is handled off the accept loop: the callback route calls back into
            // this same server for the token exchange, which would deadlock a serial loop.
            _ = HandleAsync(context);
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        try
        {
            await RouteAsync(context);
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or IOException or HttpRequestException)
        {
            Trace.TraceWarning($"[OAuthTestServer] request failed: {ex.Message}");
        }
    }

    private async Task RouteAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath ?? "/";
        var query = ParseQuery(context.Request.Url?.Query);

        if (path == "/signin")
        {
            StartSignIn(context);
            return;
        }

        if (path == "/oauth/authorize")
        {
            Authorize(context, query);
            return;
        }

        if (path == "/oauth/callback")
        {
            await CompleteSignInAsync(context, query);
            return;
        }

        if (path == "/oauth/token")
        {
            await IssueTokenAsync(context);
            return;
        }

        if (path == "/session")
        {
            await WriteSessionAsync(context);
            return;
        }

        await WritePageAsync(context, query);
    }

    // The application starts the flow: it keeps the PKCE verifier and sends the browser to the
    // provider with only the derived challenge.
    private void StartSignIn(HttpListenerContext context)
    {
        var verifier = RandomToken(48);
        var state = RandomToken(16);
        _pendingVerifiers[state] = verifier;

        var authorize = $"{BaseUrl}oauth/authorize"
                        + "?response_type=code"
                        + $"&client_id={Uri.EscapeDataString(ClientId)}"
                        + $"&redirect_uri={Uri.EscapeDataString($"{BaseUrl}oauth/callback")}"
                        + "&scope=profile"
                        + $"&state={Uri.EscapeDataString(state)}"
                        + $"&code_challenge={Uri.EscapeDataString(Challenge(verifier))}"
                        + "&code_challenge_method=S256";
        context.Response.Redirect(authorize);
        context.Response.Close();
    }

    // The provider approves the already-consented client and hands back a one-time code bound
    // to the PKCE challenge.
    private void Authorize(HttpListenerContext context, IReadOnlyDictionary<string, string> query)
    {
        _authorizeRequests.Enqueue(query);
        _authorizeSignal.Release();

        var redirectUri = query.GetValueOrDefault("redirect_uri", string.Empty);
        var valid = query.GetValueOrDefault("response_type") == "code"
                    && query.GetValueOrDefault("client_id") == ClientId
                    && query.GetValueOrDefault("code_challenge_method") == "S256"
                    && query.GetValueOrDefault("code_challenge", string.Empty).Length > 0
                    && redirectUri.StartsWith(BaseUrl, StringComparison.Ordinal);
        if (!valid)
        {
            Reject(context, "invalid_request");
            return;
        }

        var code = RandomToken(24);
        _issuedCodes[code] = query["code_challenge"];
        context.Response.Redirect(
            $"{redirectUri}?code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(query.GetValueOrDefault("state", string.Empty))}");
        context.Response.Close();
    }

    // The application's redirect endpoint: it matches the state it issued, exchanges the code on
    // the back channel, and only then puts a session cookie in the browser.
    private async Task CompleteSignInAsync(HttpListenerContext context, IReadOnlyDictionary<string, string> query)
    {
        var state = query.GetValueOrDefault("state", string.Empty);
        var code = query.GetValueOrDefault("code", string.Empty);
        if (!_pendingVerifiers.TryRemove(state, out var verifier) || code.Length == 0)
        {
            Reject(context, "invalid_state");
            return;
        }

        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = $"{BaseUrl}oauth/callback",
            ["client_id"] = ClientId,
            ["code_verifier"] = verifier
        });
        using var response = await _backChannel.PostAsync("oauth/token", form);
        if (!response.IsSuccessStatusCode)
        {
            Reject(context, "token_exchange_failed");
            return;
        }

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!payload.RootElement.TryGetProperty("access_token", out var token)
            || string.IsNullOrEmpty(token.GetString()))
        {
            Reject(context, "no_access_token");
            return;
        }

        var session = RandomToken(24);
        _sessions[session] = SignedInUser;
        context.Response.AppendCookie(new Cookie(SessionCookieName, session) { Path = "/" });
        context.Response.Redirect(BaseUrl);
        context.Response.Close();
    }

    private async Task IssueTokenAsync(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        var form = ParseForm(await reader.ReadToEndAsync());
        var code = form.GetValueOrDefault("code", string.Empty);
        var verifier = form.GetValueOrDefault("code_verifier", string.Empty);
        var grantIsValid = form.GetValueOrDefault("grant_type") == "authorization_code"
                           && form.GetValueOrDefault("client_id") == ClientId
                           && _issuedCodes.TryRemove(code, out var challenge)
                           && string.Equals(challenge, Challenge(verifier), StringComparison.Ordinal);
        if (!grantIsValid)
        {
            Reject(context, "invalid_grant");
            return;
        }

        Interlocked.Increment(ref _tokenGrants);
        await WriteJsonAsync(context, $$"""
            {"access_token":"{{RandomToken(32)}}","token_type":"Bearer","expires_in":3600,"scope":"profile"}
            """);
    }

    private Task WriteSessionAsync(HttpListenerContext context)
    {
        var user = CurrentUser(context.Request);
        var body = user is null ? """{"user":null}""" : $$"""{"user":"{{user}}"}""";
        return WriteJsonAsync(context, body);
    }

    private async Task WritePageAsync(HttpListenerContext context, IReadOnlyDictionary<string, string> query)
    {
        var signingOut = query.ContainsKey("signout");
        if (signingOut)
            SignOut(context);

        var user = signingOut ? null : CurrentUser(context.Request);
        var page = PageTemplate
            .Replace("__AGENTUP_STATUS__", user is null ? "signed-out" : $"signed-in:{user}", StringComparison.Ordinal)
            .Replace("__AGENTUP_NAV__", query.GetValueOrDefault("nav", string.Empty), StringComparison.Ordinal);

        var body = Encoding.UTF8.GetBytes(page);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
    }

    private void SignOut(HttpListenerContext context)
    {
        var session = context.Request.Cookies[SessionCookieName]?.Value;
        if (session is not null)
            _sessions.TryRemove(session, out _);

        // Replacing the cookie with an unknown session id signs the browser out without relying
        // on cookie-deletion semantics differing between the platform WebView engines.
        context.Response.AppendCookie(new Cookie(SessionCookieName, RandomToken(8)) { Path = "/" });
    }

    private string? CurrentUser(HttpListenerRequest request)
    {
        var session = request.Cookies[SessionCookieName]?.Value;
        return session is not null && _sessions.TryGetValue(session, out var user) ? user : null;
    }

    private void Reject(HttpListenerContext context, string error)
    {
        Interlocked.Increment(ref _rejectedRequests);
        Trace.TraceWarning($"[OAuthTestServer] rejected request: {error}");
        context.Response.StatusCode = 400;
        context.Response.Close();
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
    }

    private static string Challenge(string verifier)
        => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string RandomToken(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    private static string Base64Url(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static IReadOnlyDictionary<string, string> ParseQuery(string? query)
        => ParseForm((query ?? string.Empty).TrimStart('?'));

    private static IReadOnlyDictionary<string, string> ParseForm(string encoded)
        => encoded
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .GroupBy(parts => Uri.UnescapeDataString(parts[0]), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(parts => parts.Length > 1 ? Uri.UnescapeDataString(parts[1].Replace('+', ' ')) : string.Empty).First(),
                StringComparer.Ordinal);

    public void Dispose()
    {
        try
        {
            _listener.Stop();
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException)
        {
            Trace.TraceWarning(ex.Message);
        }

        _authorizeSignal.Dispose();
        _backChannel.Dispose();
    }
}

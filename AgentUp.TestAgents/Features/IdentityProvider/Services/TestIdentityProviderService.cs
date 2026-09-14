using System.Net;
using System.Text;
using System.Text.Json;
using AgentUp.TestAgents.Features.IdentityProvider.Models;
using AgentUp.TestAgents.Features.IdentityProvider.Providers;
using AgentUp.TestAgents.Shared.Providers;

namespace AgentUp.TestAgents.Features.IdentityProvider.Services;

/// <summary>
/// The identity provider the test agents sign in against: a real HTTP deployment speaking real
/// OAuth 2.0, covering every shape the vendor CLIs use — authorization code with PKCE, device
/// authorization, and poll-until-approved.
/// <para>
/// It also exposes a control plane under <c>/test</c>. An end-to-end test approves a specific
/// sign-in through it rather than waiting for a poll interval or driving a browser's DOM, which
/// is what keeps those tests deterministic.
/// </para>
/// </summary>
public sealed class TestIdentityProviderService : IAsyncDisposable
{
    private static readonly TimeSpan GrantLifetime = TimeSpan.FromMinutes(15);

    private readonly HttpListener _listener = new();
    private readonly TestIdentityStore _store = new();
    private readonly string _publicOrigin;
    private Task? _accepting;

    public TestIdentityProviderService(int port, string? publicOrigin)
    {
        Port = port == 0 ? FreePort() : port;
        // Bound on every interface: an Android emulator reaches its host as 10.0.2.2 and an iOS
        // simulator as localhost, and the same provider has to answer both.
        _listener.Prefixes.Add($"http://+:{Port}/");
        _publicOrigin = (publicOrigin ?? $"http://localhost:{Port}").TrimEnd('/');
    }

    public int Port { get; }

    /// <summary>The origin to write into links, which is not always the one it bound.</summary>
    public string PublicOrigin => _publicOrigin;

    public void Start()
    {
        try
        {
            _listener.Start();
        }
        catch (HttpListenerException)
        {
            // Binding every interface needs a URL reservation on Windows; loopback always works.
            _listener.Prefixes.Clear();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Start();
        }

        _accepting = AcceptAsync();
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
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            _ = RouteAsync(context);
        }
    }

    private async Task RouteAsync(HttpListenerContext context)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "/";
            var task = path switch
            {
                "/oauth/authorize" => AuthorizeAsync(context),
                "/oauth/approve" => ApproveAuthorizationAsync(context),
                "/oauth/token" => TokenAsync(context),
                "/oauth/device/code" => DeviceCodeAsync(context),
                "/oauth/device/token" => DeviceTokenAsync(context),
                "/login/start" => StartLoginAsync(context),
                "/device" => DeviceEntryAsync(context),
                "/device/approve" => DeviceApproveAsync(context),
                "/test/pre-approve" => PreApproveAsync(context),
                "/test/approve" => TestApproveAsync(context),
                "/test/latest-code" => LatestCodeAsync(context),
                "/test/reset" => ResetAsync(context),
                "/health" => WriteJsonAsync(context, 200, new { status = "ok" }),
                _ => RouteLoginAsync(context, path)
            };
            await task;
        }
        catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or IOException)
        {
            return;
        }
    }

    private Task RouteLoginAsync(HttpListenerContext context, string path)
    {
        // /login/{id}, /login/{id}/approve, /login/{id}/status
        var segments = path.Trim('/').Split('/');
        if (segments.Length < 2 || !segments[0].Equals("login", StringComparison.Ordinal))
            return WriteHtmlAsync(context, 404, TestIdentityPages.Problem("No such page."));

        var loginId = segments[1];
        if (segments.Length == 2)
            return LoginPageAsync(context, loginId);
        if (segments[2].Equals("approve", StringComparison.Ordinal))
            return LoginApproveAsync(context, loginId);
        if (segments[2].Equals("status", StringComparison.Ordinal))
            return LoginStatusAsync(context, loginId);
        return WriteHtmlAsync(context, 404, TestIdentityPages.Problem("No such page."));
    }

    // ---- authorization code with PKCE -------------------------------------------------

    private Task AuthorizeAsync(HttpListenerContext context)
    {
        var query = Query(context);
        var clientId = query.GetValueOrDefault("client_id");
        if (string.IsNullOrWhiteSpace(clientId))
            return WriteHtmlAsync(context, 400, TestIdentityPages.Problem("client_id is required."));
        if (!string.Equals(query.GetValueOrDefault("code_challenge_method"), "S256", StringComparison.Ordinal))
            return WriteHtmlAsync(context, 400, TestIdentityPages.Problem("PKCE S256 is required."));

        var pending = new PendingAuthorization(
            clientId,
            query.GetValueOrDefault("redirect_uri"),
            query.GetValueOrDefault("state"),
            query.GetValueOrDefault("code_challenge"));

        // A test that pre-approved this client gets the redirect immediately, so the agent's real
        // callback path runs without anything having to click a button.
        if (_store.IsPreApproved(clientId))
            return CompleteAuthorizationAsync(context, pending);

        var hidden = new StringBuilder();
        foreach (var name in new[] { "client_id", "redirect_uri", "state", "code_challenge" })
            hidden.Append($"<input type=\"hidden\" name=\"{name}\" value=\"{WebUtility.HtmlEncode(query.GetValueOrDefault(name) ?? string.Empty)}\" />");
        return WriteHtmlAsync(context, 200, TestIdentityPages.Consent(clientId, "/oauth/approve", hidden.ToString()));
    }

    private async Task ApproveAuthorizationAsync(HttpListenerContext context)
    {
        var form = await FormAsync(context);
        var clientId = form.GetValueOrDefault("client_id");
        if (string.IsNullOrWhiteSpace(clientId))
        {
            await WriteHtmlAsync(context, 400, TestIdentityPages.Problem("client_id is required."));
            return;
        }

        await CompleteAuthorizationAsync(context, new PendingAuthorization(
            clientId,
            form.GetValueOrDefault("redirect_uri"),
            form.GetValueOrDefault("state"),
            form.GetValueOrDefault("code_challenge")));
    }

    private Task CompleteAuthorizationAsync(HttpListenerContext context, PendingAuthorization pending)
    {
        var code = _store.StartAuthorization(pending);

        // No redirect_uri means the code is carried by hand: show it for the user to copy, the
        // way the real console page does for `claude setup-token`.
        if (string.IsNullOrWhiteSpace(pending.RedirectUri))
            return WriteHtmlAsync(context, 200, TestIdentityPages.CodeToCopy(
                string.IsNullOrWhiteSpace(pending.State) ? code : $"{code}#{pending.State}"));

        var separator = pending.RedirectUri.Contains('?') ? '&' : '?';
        var location = $"{pending.RedirectUri}{separator}code={Uri.EscapeDataString(code)}";
        if (!string.IsNullOrWhiteSpace(pending.State))
            location += $"&state={Uri.EscapeDataString(pending.State)}";
        context.Response.StatusCode = 302;
        context.Response.Headers["Location"] = location;
        context.Response.Close();
        return Task.CompletedTask;
    }

    private async Task TokenAsync(HttpListenerContext context)
    {
        var form = await FormAsync(context);
        var code = form.GetValueOrDefault("code");
        if (string.IsNullOrWhiteSpace(code))
        {
            await WriteJsonAsync(context, 400, new { error = "invalid_request" });
            return;
        }

        var pending = _store.RedeemAuthorization(code);
        if (pending is null)
        {
            await WriteJsonAsync(context, 400, new { error = "invalid_grant" });
            return;
        }

        if (!PkceVerifier.Matches(pending.CodeChallenge, form.GetValueOrDefault("code_verifier")))
        {
            await WriteJsonAsync(context, 400, new { error = "invalid_grant", error_description = "PKCE verification failed." });
            return;
        }

        var token = _store.Issue(pending.ClientId);
        await WriteJsonAsync(context, 200, new { access_token = token.Value, token_type = "Bearer", expires_in = 3600 });
    }

    // ---- device authorization ---------------------------------------------------------

    private async Task DeviceCodeAsync(HttpListenerContext context)
    {
        var form = await FormAsync(context);
        var clientId = form.GetValueOrDefault("client_id") ?? "test-agent";
        var (deviceCode, grant) = _store.StartDevice(clientId, GrantLifetime);
        await WriteJsonAsync(context, 200, new
        {
            device_code = deviceCode,
            user_code = grant.UserCode,
            verification_uri = $"{_publicOrigin}/device",
            verification_uri_complete = $"{_publicOrigin}/device?user_code={Uri.EscapeDataString(grant.UserCode)}",
            expires_in = (int)GrantLifetime.TotalSeconds,
            interval = 1
        });
    }

    private async Task DeviceTokenAsync(HttpListenerContext context)
    {
        var form = await FormAsync(context);
        var deviceCode = form.GetValueOrDefault("device_code");
        var grant = deviceCode is null ? null : _store.Device(deviceCode);
        if (grant is null)
        {
            await WriteJsonAsync(context, 400, new { error = "invalid_grant" });
            return;
        }

        if (DateTimeOffset.UtcNow > grant.ExpiresAt)
        {
            await WriteJsonAsync(context, 400, new { error = "expired_token" });
            return;
        }

        if (!grant.Approved)
        {
            await WriteJsonAsync(context, 400, new { error = "authorization_pending" });
            return;
        }

        var token = _store.Issue(grant.ClientId);
        await WriteJsonAsync(context, 200, new { access_token = token.Value, token_type = "Bearer", expires_in = 3600 });
    }

    private Task DeviceEntryAsync(HttpListenerContext context) =>
        WriteHtmlAsync(context, 200, TestIdentityPages.DeviceEntry("/device/approve"));

    private async Task DeviceApproveAsync(HttpListenerContext context)
    {
        var form = await FormAsync(context);
        var userCode = form.GetValueOrDefault("user_code");
        var deviceCode = userCode is null ? null : _store.DeviceCodeFor(userCode);
        if (deviceCode is null || !_store.ApproveDevice(deviceCode))
        {
            await WriteHtmlAsync(context, 400, TestIdentityPages.Problem("That code is not valid."));
            return;
        }

        await WriteHtmlAsync(context, 200, TestIdentityPages.Done("You can go back to Agent-Up."));
    }

    // ---- poll until approved ----------------------------------------------------------

    private async Task StartLoginAsync(HttpListenerContext context)
    {
        var form = await FormAsync(context);
        var clientId = form.GetValueOrDefault("client_id") ?? "test-agent";
        var loginId = _store.StartLogin(clientId, GrantLifetime);
        await WriteJsonAsync(context, 200, new
        {
            login_id = loginId,
            // The opaque deep link the agent prints. No user code is ever shown for this shape.
            login_url = $"{_publicOrigin}/login/{loginId}",
            status_url = $"{_publicOrigin}/login/{loginId}/status",
            expires_in = (int)GrantLifetime.TotalSeconds
        });
    }

    private Task LoginPageAsync(HttpListenerContext context, string loginId)
    {
        if (_store.Login(loginId) is null)
            return WriteHtmlAsync(context, 404, TestIdentityPages.Problem("That sign-in has expired."));
        return WriteHtmlAsync(context, 200, TestIdentityPages.Consent("Agent-Up", $"/login/{loginId}/approve", string.Empty));
    }

    private Task LoginApproveAsync(HttpListenerContext context, string loginId) =>
        _store.ApproveLogin(loginId)
            ? WriteHtmlAsync(context, 200, TestIdentityPages.Done("You can go back to Agent-Up."))
            : WriteHtmlAsync(context, 404, TestIdentityPages.Problem("That sign-in has expired."));

    private Task LoginStatusAsync(HttpListenerContext context, string loginId)
    {
        var grant = _store.Login(loginId);
        if (grant is null)
            return WriteJsonAsync(context, 404, new { status = "unknown" });
        if (DateTimeOffset.UtcNow > grant.ExpiresAt)
            return WriteJsonAsync(context, 200, new { status = "expired" });
        if (!grant.Approved)
            return WriteJsonAsync(context, 200, new { status = "pending" });
        return WriteJsonAsync(context, 200, new { status = "approved", access_token = _store.Issue(grant.ClientId).Value });
    }

    // ---- test control plane -----------------------------------------------------------

    private async Task PreApproveAsync(HttpListenerContext context)
    {
        var form = await FormAsync(context);
        var clientId = form.GetValueOrDefault("client_id");
        if (string.IsNullOrWhiteSpace(clientId))
        {
            await WriteJsonAsync(context, 400, new { error = "client_id is required" });
            return;
        }

        _store.PreApprove(clientId);
        await WriteJsonAsync(context, 200, new { preApproved = clientId });
    }

    private async Task TestApproveAsync(HttpListenerContext context)
    {
        var form = await FormAsync(context);
        var userCode = form.GetValueOrDefault("user_code");
        if (!string.IsNullOrWhiteSpace(userCode))
        {
            var deviceCode = _store.DeviceCodeFor(userCode);
            var approved = deviceCode is not null && _store.ApproveDevice(deviceCode);
            await WriteJsonAsync(context, approved ? 200 : 404, new { approved });
            return;
        }

        var loginId = form.GetValueOrDefault("login_id");
        if (!string.IsNullOrWhiteSpace(loginId))
        {
            var approved = _store.ApproveLogin(loginId);
            await WriteJsonAsync(context, approved ? 200 : 404, new { approved });
            return;
        }

        await WriteJsonAsync(context, 400, new { error = "user_code or login_id is required" });
    }

    private Task LatestCodeAsync(HttpListenerContext context)
    {
        var clientId = Query(context).GetValueOrDefault("client_id");
        if (string.IsNullOrWhiteSpace(clientId))
            return WriteJsonAsync(context, 400, new { error = "client_id is required" });
        var code = _store.LatestCode(clientId);
        return code is null
            ? WriteJsonAsync(context, 404, new { error = "no code has been issued" })
            : WriteJsonAsync(context, 200, new { code });
    }

    private Task ResetAsync(HttpListenerContext context)
    {
        _store.Reset();
        return WriteJsonAsync(context, 200, new { reset = true });
    }

    // ---- plumbing ---------------------------------------------------------------------

    private static IReadOnlyDictionary<string, string> Query(HttpListenerContext context) =>
        FormReader.Query(context.Request.Url?.Query);

    private static async Task<IReadOnlyDictionary<string, string>> FormAsync(HttpListenerContext context)
    {
        using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
        return FormReader.Parse(await reader.ReadToEndAsync());
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, int status, object payload)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(payload);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
    }

    private static async Task WriteHtmlAsync(HttpListenerContext context, int status, string html)
    {
        var body = Encoding.UTF8.GetBytes(html);
        context.Response.StatusCode = status;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body);
        context.Response.Close();
    }

    private static int FreePort()
    {
        using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public async ValueTask DisposeAsync()
    {
        StopListening();
        await DrainAsync();
    }

    private void StopListening()
    {
        try
        {
            _listener.Stop();
            _listener.Close();
        }
        catch (Exception exception) when (exception is ObjectDisposedException or HttpListenerException)
        {
            // Already torn down, so there is nothing left to stop. The accept loop is still
            // drained by the caller.
            return;
        }
    }

    private async Task DrainAsync()
    {
        if (_accepting is null)
            return;

        try
        {
            await _accepting;
        }
        catch (Exception exception) when (exception is OperationCanceledException or HttpListenerException or ObjectDisposedException)
        {
            return;
        }
    }
}

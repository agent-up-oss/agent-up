using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AgentUp.TestAgents.Features.Authentication.Interfaces;
using AgentUp.TestAgents.Features.IdentityProvider.Providers;

namespace AgentUp.TestAgents.Features.Authentication.Providers;

/// <summary>
/// test-agent1: authorization code with PKCE answered on a loopback listener the agent opens
/// itself, the shape <c>codex login</c> uses by default.
/// <para>
/// This is the shape that breaks when the browser is not on the agent's host: the listener is
/// bound here, so the redirect only ever arrives if something carries it back. The Server's
/// callback relay is what does that, and this agent is how that path gets exercised.
/// </para>
/// </summary>
public sealed class LoopbackRedirectLoginFlow(HttpClient client, string identityProviderUrl) : ITestAgentLoginFlow
{
    public string ClientId => "test-agent1";

    public async Task<string?> RunAsync(TextWriter output, TextReader input, CancellationToken cancellationToken)
    {
        var verifier = PkceVerifier.Secret();
        var state = PkceVerifier.Secret(16);
        var port = FreePort();
        var redirectUri = $"http://localhost:{port}/auth/callback";

        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/auth/callback/");
        listener.Prefixes.Add($"http://127.0.0.1:{port}/auth/callback/");
        listener.Start();

        try
        {
            var authorize =
                $"{identityProviderUrl}/oauth/authorize" +
                $"?response_type=code&client_id={Uri.EscapeDataString(ClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&state={Uri.EscapeDataString(state)}" +
                $"&code_challenge={Uri.EscapeDataString(PkceVerifier.Challenge(verifier))}" +
                "&code_challenge_method=S256";

            await output.WriteLineAsync("Sign in with your subscription:");
            await output.WriteLineAsync($"  {authorize}");
            await output.WriteLineAsync($"Waiting for the sign-in to come back to {redirectUri}");
            await output.FlushAsync(cancellationToken);

            var code = await AwaitCallbackAsync(listener, state, output, cancellationToken);
            if (code is null)
                return null;

            using var response = await client.PostAsync(
                $"{identityProviderUrl}/oauth/token",
                new FormUrlEncodedContent([
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("client_id", ClientId),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("code_verifier", verifier)
                ]),
                cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var error = payload.TryGetProperty("error", out var value) ? value.GetString() : "unknown_error";
                await output.WriteLineAsync($"Sign-in failed: {error}.");
                return null;
            }

            await output.WriteLineAsync("Signed in.");
            return payload.GetProperty("access_token").GetString();
        }
        finally
        {
            listener.Stop();
        }
    }

    private static async Task<string?> AwaitCallbackAsync(
        HttpListener listener,
        string state,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        HttpListenerContext context;
        try
        {
            context = await listener.GetContextAsync().WaitAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
        {
            await output.WriteLineAsync("The sign-in listener closed before the redirect arrived.");
            return null;
        }

        var query = FormReader.Query(context.Request.Url?.Query);
        var code = query.GetValueOrDefault("code");
        var returned = query.GetValueOrDefault("state");
        var ok = !string.IsNullOrWhiteSpace(code) && string.Equals(returned, state, StringComparison.Ordinal);

        var body = Encoding.UTF8.GetBytes(ok
            ? "<!DOCTYPE html><html><body><p id=\"status\">Signed in. You can close this window.</p></body></html>"
            : "<!DOCTYPE html><html><body><p id=\"status\">That sign-in could not be completed.</p></body></html>");
        context.Response.StatusCode = ok ? 200 : 400;
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body, cancellationToken);
        context.Response.Close();

        if (ok)
            return code;

        await output.WriteLineAsync("The sign-in redirect did not carry a matching state.");
        return null;
    }

    private static int FreePort()
    {
        using var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using AgentUp.TestAgents.Features.Authentication.Interfaces;
using AgentUp.TestAgents.Shared.Providers;

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
/// <param name="publicOrigin">
/// Where the person opening the link reaches the identity provider, when that is somewhere other
/// than where this agent reaches it - an emulator, say, for which the host is 10.0.2.2 and
/// localhost is the emulator itself. Only the link printed for them uses it; everything this agent
/// fetches keeps going to <paramref name="identityProviderUrl"/>, which is the one it can route to.
/// Defaults to that, so a setup where the two are the same says nothing about it.
/// </param>
public sealed class LoopbackRedirectLoginFlow(
    HttpClient client,
    string identityProviderUrl,
    string? publicOrigin = null) : ITestAgentLoginFlow
{
    public string ClientId => "test-agent1";

    public async Task<string?> RunAsync(TextWriter output, TextReader input, CancellationToken cancellationToken)
    {
        var verifier = PkceVerifier.Secret();
        var state = PkceVerifier.Secret(16);
        // The listener claims the port itself. Picking a free one and binding it a moment later is
        // a race against everything else on the machine, and losing it fails a sign-in that was
        // never wrong.
        var (listener, port) = LoopbackListener.Start(0, Callback);
        var redirectUri = $"http://localhost:{port}/auth/callback";

        try
        {
            var authorize =
                $"{publicOrigin ?? identityProviderUrl}/oauth/authorize" +
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

            using var request = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("grant_type", "authorization_code"),
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("code", code),
                new KeyValuePair<string, string>("code_verifier", verifier)
            ]);
            using var response = await client.PostAsync(
                $"{identityProviderUrl}/oauth/token",
                request,
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
            listener.Close();
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

    private static IEnumerable<string> Callback(int port) =>
        [$"http://localhost:{port}/auth/callback/", $"http://127.0.0.1:{port}/auth/callback/"];
}

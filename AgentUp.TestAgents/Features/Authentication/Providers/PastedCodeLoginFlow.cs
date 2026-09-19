using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.TestAgents.Features.Authentication.Interfaces;
using AgentUp.TestAgents.Shared.Providers;

namespace AgentUp.TestAgents.Features.Authentication.Providers;

/// <summary>
/// test-agent3: the shape <c>claude setup-token</c> uses.
/// <para>
/// Prints a link, then writes a prompt <em>without a trailing newline</em> and blocks on stdin
/// for the code the user copies out of the browser. That unterminated prompt is deliberate: it is
/// exactly what deadlocks a line-oriented reader, and reproducing it here is the point of this
/// agent existing.
/// </para>
/// </summary>
/// <param name="publicOrigin">
/// Where the person opening the link reaches the identity provider, when that differs from where
/// this agent reaches it. Only the printed link uses it; the token exchange below stays on
/// <paramref name="identityProviderUrl"/>. Defaults to that.
/// </param>
public sealed class PastedCodeLoginFlow(
    HttpClient client,
    string identityProviderUrl,
    string? publicOrigin = null) : ITestAgentLoginFlow
{
    public string ClientId => "test-agent3";

    public async Task<string?> RunAsync(TextWriter output, TextReader input, CancellationToken cancellationToken)
    {
        var verifier = PkceVerifier.Secret();
        var state = PkceVerifier.Secret(16);
        var authorize =
            $"{publicOrigin ?? identityProviderUrl}/oauth/authorize" +
            $"?response_type=code&client_id={Uri.EscapeDataString(ClientId)}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&code_challenge={Uri.EscapeDataString(PkceVerifier.Challenge(verifier))}" +
            "&code_challenge_method=S256";

        await output.WriteLineAsync("Sign in with your subscription:");
        await output.WriteLineAsync($"  {authorize}");
        await output.WriteLineAsync("Then copy the code the page shows you.");

        // No newline, then block. This is the prompt shape that broke the Server's old reader.
        await output.WriteAsync("Paste code here: ");
        await output.FlushAsync(cancellationToken);

        var pasted = await input.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(pasted))
        {
            await output.WriteLineAsync();
            await output.WriteLineAsync("No code was provided.");
            return null;
        }

        await output.WriteLineAsync();

        // The page renders "code#state"; the CLI splits it back apart.
        var separator = pasted.IndexOf('#');
        var code = separator < 0 ? pasted.Trim() : pasted[..separator].Trim();

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

        var token = payload.GetProperty("access_token").GetString();

        // The real CLI prints the token it minted; the Server picks it up from here.
        await output.WriteLineAsync($"sk-ant-oat01-{token}");
        return $"sk-ant-oat01-{token}";
    }
}

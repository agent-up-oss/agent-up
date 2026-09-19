using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.TestAgents.Features.Authentication.Interfaces;

namespace AgentUp.TestAgents.Features.Authentication.Providers;

/// <summary>
/// test-agent2: device authorization, the shape <c>codex login --device-auth</c> uses.
/// <para>
/// Prints a verification URL and a short user code, then polls the token endpoint. Nothing
/// listens on a socket, so this is the shape that works unchanged when the browser is on a
/// different device from the agent.
/// </para>
/// </summary>
public sealed class DeviceCodeLoginFlow(HttpClient client, string identityProviderUrl) : ITestAgentLoginFlow
{
    public string ClientId => "test-agent2";

    public async Task<string?> RunAsync(TextWriter output, TextReader input, CancellationToken cancellationToken)
    {
        using var request = new FormUrlEncodedContent([new KeyValuePair<string, string>("client_id", ClientId)]);
        using var started = await client.PostAsync(
            $"{identityProviderUrl}/oauth/device/code",
            request,
            cancellationToken);
        if (!started.IsSuccessStatusCode)
        {
            await output.WriteLineAsync("Could not start device authorization.");
            return null;
        }

        var grant = await started.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var deviceCode = grant.GetProperty("device_code").GetString()!;
        var userCode = grant.GetProperty("user_code").GetString()!;
        var verificationUri = grant.GetProperty("verification_uri").GetString()!;
        var interval = TimeSpan.FromSeconds(Math.Max(1, grant.GetProperty("interval").GetInt32()));
        var expiresIn = grant.GetProperty("expires_in").GetInt32();

        // The wording matters: the Server reads the link and the code out of this output, so it
        // has to look like what a real CLI prints.
        await output.WriteLineAsync("Follow these steps to sign in with your subscription.");
        await output.WriteLineAsync($"  Open: {verificationUri}");
        await output.WriteLineAsync($"  Enter this one-time code: {userCode}");
        await output.WriteLineAsync($"  The code expires in {expiresIn} seconds.");
        await output.FlushAsync(cancellationToken);

        return await PollAsync(deviceCode, interval, output, cancellationToken);
    }

    private async Task<string?> PollAsync(string deviceCode, TimeSpan interval, TextWriter output, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            using var request = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("device_code", deviceCode)
            ]);
            using var response = await client.PostAsync(
                $"{identityProviderUrl}/oauth/device/token",
                request,
                cancellationToken);

            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (response.IsSuccessStatusCode)
                return payload.GetProperty("access_token").GetString();

            var error = payload.TryGetProperty("error", out var value) ? value.GetString() : "unknown_error";
            if (error == "authorization_pending")
            {
                await Task.Delay(interval, cancellationToken);
                continue;
            }

            await output.WriteLineAsync($"Sign-in failed: {error}.");
            return null;
        }

        return null;
    }
}

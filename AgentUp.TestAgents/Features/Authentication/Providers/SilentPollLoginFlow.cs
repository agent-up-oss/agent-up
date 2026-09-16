using System.Net.Http.Json;
using System.Text.Json;
using AgentUp.TestAgents.Features.Authentication.Interfaces;

namespace AgentUp.TestAgents.Features.Authentication.Providers;

/// <summary>
/// test-agent4: the shape <c>cursor-agent login</c> uses.
/// <para>
/// Prints one opaque deep link tied to a login id and then polls quietly until it is approved.
/// No user code is ever shown and nothing is carried back by hand, so the client's only job is to
/// open the link. It writes spinner-style progress the way the real CLI does, which is also what
/// makes it useful for proving the Server does not mistake progress noise for a code.
/// </para>
/// </summary>
public sealed class SilentPollLoginFlow(HttpClient client, string identityProviderUrl) : ITestAgentLoginFlow
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    public string ClientId => "test-agent4";

    public async Task<string?> RunAsync(TextWriter output, TextReader input, CancellationToken cancellationToken)
    {
        using var request = new FormUrlEncodedContent([new KeyValuePair<string, string>("client_id", ClientId)]);
        using var started = await client.PostAsync(
            $"{identityProviderUrl}/login/start",
            request,
            cancellationToken);
        if (!started.IsSuccessStatusCode)
        {
            await output.WriteLineAsync("Could not start the sign-in.");
            return null;
        }

        var grant = await started.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        // The link is for the person, so it comes back on whatever origin they reach the provider
        // at. The status URL comes back on that same origin and is for this agent, which may not
        // be able to route to it at all - on an Android emulator the person's origin is 10.0.2.2,
        // and nothing on the host answers there. So the path is kept and the base is not: polling
        // goes where this agent already knows it can reach.
        var loginUrl = grant.GetProperty("login_url").GetString()!;
        var statusUrl = Reachable(grant.GetProperty("status_url").GetString()!);

        await output.WriteLineAsync("Failed to open browser for login. Please visit:");
        await output.WriteLineAsync($"  {loginUrl}");
        await output.FlushAsync(cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            using var response = await client.GetAsync(statusUrl, cancellationToken);
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var status = payload.TryGetProperty("status", out var value) ? value.GetString() : "unknown";

            if (status == "approved")
            {
                await output.WriteLineAsync("Signed in.");
                return payload.GetProperty("access_token").GetString();
            }

            if (status != "pending")
            {
                await output.WriteLineAsync($"Sign-in failed: {status}.");
                return null;
            }

            // Progress noise, on purpose: a tracking code here must not be mistaken for a code
            // the user is meant to type.
            await output.WriteLineAsync("Waiting for approval, tracking code POLL-000001");
            await output.FlushAsync(cancellationToken);
            await Task.Delay(Interval, cancellationToken);
        }

        return null;
    }

    /// <summary>The same endpoint, on the origin this agent can reach rather than the one it was told.</summary>
    private string Reachable(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var absolute)
            ? $"{identityProviderUrl}{absolute.PathAndQuery}"
            : url;
}

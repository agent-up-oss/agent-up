using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace AgentUp.Desktop.Features.Authentication.Providers;

public static class ServerSessionProvider
{
    private static readonly ConditionalWeakTable<HttpClient, ServerSessionHandler> Sessions = new();

    public static HttpClient CreateClient(Uri serverUri, HttpMessageHandler? inner = null, string? accessToken = null)
    {
        SecureServerUrlProvider.EnsureCredentialTransportAllowed(serverUri);
        var session = new ServerSessionHandler(inner ?? new HttpClientHandler());
        session.Apply(serverUri, accessToken);
        var http = new HttpClient(session) { BaseAddress = serverUri };
        Sessions.Add(http, session);
        ApplyAuthorizationHeader(http, accessToken);
        return http;
    }

    public static void Apply(HttpClient http, Uri uri, string? accessToken)
    {
        if (Sessions.TryGetValue(http, out var session))
        {
            session.Apply(uri, accessToken);
            if (http.BaseAddress is null)
                http.BaseAddress = uri;
        }
        else
        {
            http.BaseAddress = uri;
        }

        ApplyAuthorizationHeader(http, accessToken);
    }

    public static Uri? CurrentUri(HttpClient http)
        => Sessions.TryGetValue(http, out var session) && session.CurrentUri is not null
            ? session.CurrentUri
            : http.BaseAddress;

    private static void ApplyAuthorizationHeader(HttpClient http, string? accessToken)
        => http.DefaultRequestHeaders.Authorization = string.IsNullOrWhiteSpace(accessToken)
            ? null
            : new AuthenticationHeaderValue("Bearer", accessToken);
}

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;

namespace AgentUp.Desktop.Tests.Support;

internal static class HttpTestResponses
{
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
    internal static HttpResponseMessage Json(HttpStatusCode statusCode, object payload)
        => new(statusCode) { Content = JsonContent.Create(payload) };

    internal static HttpResponseMessage Json(object payload)
        => Json(HttpStatusCode.OK, payload);

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
    internal static HttpResponseMessage Text(HttpStatusCode statusCode, string body, string mediaType = "application/json")
        => new(statusCode) { Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType) };

    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Returned HttpResponseMessage ownership transfers to HttpClient.")]
    internal static HttpResponseMessage Empty(HttpStatusCode statusCode)
        => new(statusCode);

    internal static bool IsConnectionRequest(HttpRequestMessage request)
        => request.Method == HttpMethod.Get
           && string.Equals(request.RequestUri?.AbsolutePath, "/api/connection", StringComparison.OrdinalIgnoreCase);

    internal static HttpResponseMessage LegacyOrJson(HttpRequestMessage request, HttpStatusCode statusCode, string json)
        => IsConnectionRequest(request) ? Empty(HttpStatusCode.NotFound) : Text(statusCode, json);

    internal static HttpResponseMessage LegacyOrPayload(HttpRequestMessage request, object payload)
        => IsConnectionRequest(request) ? Empty(HttpStatusCode.NotFound) : Json(payload);
}

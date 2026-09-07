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
}

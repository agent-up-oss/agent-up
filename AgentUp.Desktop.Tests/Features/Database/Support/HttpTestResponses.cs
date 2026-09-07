using System.Net;
using System.Net.Http.Json;

namespace AgentUp.Desktop.Tests.Features.Database.Support;

internal static class HttpTestResponses
{
    internal static HttpResponseMessage Json(HttpStatusCode statusCode, object payload)
        => new(statusCode) { Content = JsonContent.Create(payload) };

    internal static HttpResponseMessage Json(object payload)
        => Json(HttpStatusCode.OK, payload);

    internal static HttpResponseMessage Text(HttpStatusCode statusCode, string body, string mediaType = "application/json")
        => new(statusCode) { Content = new StringContent(body, System.Text.Encoding.UTF8, mediaType) };
}

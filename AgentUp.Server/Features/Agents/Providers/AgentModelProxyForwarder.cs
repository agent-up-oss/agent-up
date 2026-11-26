using System.Net;
using System.Net.Http.Headers;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Copies a sandboxed agent's HTTP request to the real model origin and streams the response
/// back. The Server holds network here so the agent process does not.
/// </summary>
public sealed class AgentModelProxyForwarder : IDisposable
{
    private static readonly HashSet<string> HopByHop = new(StringComparer.OrdinalIgnoreCase)
    {
        "Connection", "Keep-Alive", "Proxy-Authenticate", "Proxy-Authorization",
        "TE", "Trailer", "Transfer-Encoding", "Upgrade", "Host"
    };

    private readonly HttpClient _client;

    public AgentModelProxyForwarder()
        : this(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            ActivityHeadersPropagator = null,
            ConnectTimeout = TimeSpan.FromSeconds(15)
        })
    {
    }

    public AgentModelProxyForwarder(HttpMessageHandler handler) =>
        _client = new HttpClient(handler, disposeHandler: true) { Timeout = TimeSpan.FromMinutes(10) };

    public async Task ForwardAsync(
        HttpContext context,
        Uri destination,
        string? bearerToken,
        bool replaceIncomingAuthorization = false)
    {
        using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), destination);
        if (HasBody(context.Request.Method))
            request.Content = new StreamContent(context.Request.Body);

        var requestSkip = Exclusions(context.Request.Headers["Connection"]);
        foreach (var header in context.Request.Headers.Where(header => !requestSkip.Contains(header.Key)))
            CopyRequestHeader(request, header);

        if (request.Content is not null
            && request.Content.Headers.ContentType is null
            && MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var media))
            request.Content.Headers.ContentType = media;

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            request.Headers.Remove("Authorization");
            request.Headers.Remove("x-api-key");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        else if (replaceIncomingAuthorization)
        {
            request.Headers.Remove("Authorization");
            request.Headers.Remove("x-api-key");
        }

        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
        context.Response.StatusCode = (int)response.StatusCode;
        var responseSkip = Exclusions(response.Headers.TryGetValues("Connection", out var connection)
            ? string.Join(",", connection)
            : null);
        foreach (var header in response.Headers.Where(header => !responseSkip.Contains(header.Key)))
            context.Response.Headers[header.Key] = header.Value.ToArray();

        if (response.Content is not null)
        {
            foreach (var header in response.Content.Headers.Where(header => !responseSkip.Contains(header.Key)))
                context.Response.Headers[header.Key] = header.Value.ToArray();

            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
    }

    private static HashSet<string> Exclusions(string? connection)
    {
        var skip = new HashSet<string>(HopByHop, StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(connection))
            return skip;
        skip.UnionWith(connection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        return skip;
    }

    public void Dispose() => _client.Dispose();

    private static void CopyRequestHeader(HttpRequestMessage request, KeyValuePair<string, Microsoft.Extensions.Primitives.StringValues> header)
    {
        if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()))
            request.Content?.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
    }

    private static bool HasBody(string method) =>
        HttpMethods.IsPost(method)
        || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method)
        || HttpMethods.IsDelete(method);
}

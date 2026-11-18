using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AgentUp.TestAgents.Features.Inference.Providers;

/// <summary>
/// The loopback HTTP surface of the model server: <c>GET /v1/models</c> and
/// <c>POST /v1/chat/completions</c>, plain or as server-sent events. Given an API key, every
/// request without it as a bearer token is answered 401, the way a hosted endpoint would.
/// </summary>
public sealed class ModelHttpListener
{
    public async Task ServeAsync(
        int port,
        string? apiKey,
        Func<string> models,
        Func<string, Func<string, Task>, CancellationToken, Task<string?>> complete,
        Action<int> listening,
        CancellationToken cancellationToken)
    {
        var bound = port > 0 ? port : FreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{bound}/");
        listener.Prefixes.Add($"http://localhost:{bound}/");
        listener.Start();
        listening(bound);
        using var registration = cancellationToken.Register(listener.Stop);
        while (!cancellationToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException)
            {
                break;
            }

            _ = HandleAsync(context, apiKey, models, complete, cancellationToken);
        }
    }

    private static async Task HandleAsync(
        HttpListenerContext context,
        string? apiKey,
        Func<string> models,
        Func<string, Func<string, Task>, CancellationToken, Task<string?>> complete,
        CancellationToken cancellationToken)
    {
        var response = context.Response;
        try
        {
            if (!Authorized(context.Request, apiKey))
            {
                await WriteAsync(response, 401, "application/json", """{"error":{"message":"Invalid API key"}}""");
                return;
            }

            var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? "";
            if (context.Request.HttpMethod == "GET" && path.EndsWith("/models", StringComparison.Ordinal))
            {
                await WriteAsync(response, 200, "application/json", models());
                return;
            }

            if (context.Request.HttpMethod != "POST" || !path.EndsWith("/chat/completions", StringComparison.Ordinal))
            {
                await WriteAsync(response, 404, "application/json", """{"error":{"message":"Not found"}}""");
                return;
            }

            using var body = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var whole = await complete(await body.ReadToEndAsync(cancellationToken), async chunk =>
            {
                if (response.ContentType is null)
                {
                    response.StatusCode = 200;
                    response.ContentType = "text/event-stream";
                    response.SendChunked = true;
                }

                var bytes = Encoding.UTF8.GetBytes($"data: {chunk}\n\n");
                await response.OutputStream.WriteAsync(bytes, cancellationToken);
                await response.OutputStream.FlushAsync(cancellationToken);
            }, cancellationToken);
            if (whole is not null)
            {
                await WriteAsync(response, 200, "application/json", whole);
                return;
            }

            await response.OutputStream.WriteAsync("data: [DONE]\n\n"u8.ToArray(), cancellationToken);
            response.Close();
        }
        catch (JsonException exception)
        {
            await WriteAsync(response, 400, "application/json", JsonSerializer.Serialize(new { error = new { message = exception.Message } }));
        }
        catch (Exception exception) when (exception is HttpListenerException or IOException or OperationCanceledException)
        {
            response.Abort();
        }
    }

    private static bool Authorized(HttpListenerRequest request, string? apiKey) =>
        apiKey is null
        || string.Equals(request.Headers["Authorization"], $"Bearer {apiKey}", StringComparison.Ordinal);

    private static async Task WriteAsync(HttpListenerResponse response, int status, string contentType, string body)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        response.StatusCode = status;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private static int FreePort()
    {
        using var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        return ((IPEndPoint)probe.LocalEndpoint).Port;
    }
}

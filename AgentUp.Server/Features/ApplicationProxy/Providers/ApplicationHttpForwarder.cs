using System.Net;
using System.Net.Sockets;
using AgentUp.Server.Features.ApplicationProxy.Interfaces;
using AgentUp.Server.Features.ApplicationProxy.Models;
using Yarp.ReverseProxy.Forwarder;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationHttpForwarder : IApplicationHttpForwarder, IDisposable
{
    private readonly IHttpForwarder _forwarder;
    private readonly HttpMessageInvoker _client;
    private readonly HttpTransformer _transformer;
    private readonly ForwarderRequestConfig _requestConfig = new()
    {
        ActivityTimeout = TimeSpan.FromSeconds(120)
    };

    public ApplicationHttpForwarder(IHttpForwarder forwarder, HttpTransformer transformer)
    {
        _forwarder = forwarder;
        _transformer = transformer;
        _client = new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.None,
            UseCookies = false,
            ActivityHeadersPropagator = null,
            ConnectTimeout = TimeSpan.FromSeconds(15)
        });
    }

    public async Task ForwardAsync(HttpContext context, int allocatedPort)
    {
        context.Items[ApplicationProxyConstants.DestinationPortItem] = allocatedPort;
        var destination = $"http://127.0.0.1:{allocatedPort}";
        try
        {
            var error = await _forwarder.SendAsync(context, destination, _client, _requestConfig, _transformer);
            if (error is ForwarderError.None || context.Response.HasStarted)
                return;
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
        }
        catch (HttpRequestException)
        {
            WriteUnavailable(context);
        }
        catch (SocketException)
        {
            WriteUnavailable(context);
        }
        catch (IOException)
        {
            WriteUnavailable(context);
        }
    }

    public void Dispose() => _client.Dispose();

    private static void WriteUnavailable(HttpContext context)
    {
        if (context.Response.HasStarted)
            return;
        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        context.Response.Headers.CacheControl = "no-store";
    }
}

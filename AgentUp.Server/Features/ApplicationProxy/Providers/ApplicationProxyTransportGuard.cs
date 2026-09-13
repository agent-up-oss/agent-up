using System.Net;
using AgentUp.Server.Features.ApplicationProxy.Interfaces;
using Microsoft.AspNetCore.Http.Features;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationProxyTransportGuard : IApplicationProxyTransportGuard
{
    public bool AllowsCredentials(HttpContext context)
        => HasTlsConnection(context) || IsLoopbackPeer(context);

    private static bool HasTlsConnection(HttpContext context)
        => context.Features.Get<ITlsConnectionFeature>() is not null;

    private static bool IsLoopbackPeer(HttpContext context)
        => IsLoopbackHost(context.Request.Host.Host)
           && IsLoopbackAddress(context.Connection.RemoteIpAddress);

    private static bool IsLoopbackHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;
        return IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address);
    }

    private static bool IsLoopbackAddress(IPAddress? address)
        => address is not null && IPAddress.IsLoopback(address);
}

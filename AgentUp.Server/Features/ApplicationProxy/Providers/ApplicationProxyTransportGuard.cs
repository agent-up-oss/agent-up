using System.Net;
using AgentUp.Server.Features.ApplicationProxy.Interfaces;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationProxyTransportGuard : IApplicationProxyTransportGuard
{
    public bool AllowsCredentials(HttpContext context)
        => context.Request.IsHttps || (IsLoopbackHost(context.Request.Host.Host) && IsLoopbackAddress(context.Connection.RemoteIpAddress));

    private static bool IsLoopbackHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return false;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;
        return IPAddress.TryParse(host.Trim('[', ']'), out var address) && IPAddress.IsLoopback(address);
    }

    private static bool IsLoopbackAddress(IPAddress? address)
        => address is null || IPAddress.IsLoopback(address);
}

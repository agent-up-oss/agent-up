using AgentUp.Server.Features.ApplicationProxy.Services;

namespace AgentUp.Server.Features.ApplicationProxy.Controllers;

public sealed class ApplicationProxyFallbackController(ApplicationProxyService proxy)
{
    public Task ForwardFallback(HttpContext context)
        => proxy.ForwardFallbackAsync(context);
}

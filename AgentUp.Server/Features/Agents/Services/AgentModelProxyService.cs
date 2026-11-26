using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Providers;

namespace AgentUp.Server.Features.Agents.Services;

/// <summary>
/// Forwards a sandboxed agent's model HTTP through the Server. The agent names a capability
/// and a proxy key; the Server chooses the origin and holds OpenCode's token.
/// </summary>
public sealed class AgentModelProxyService(
    AgentModelProxyAccess access,
    IAgentModelProxyDestinations destinations,
    AgentModelProxyForwarder forwarder)
{
    public async Task ForwardAsync(string key, string capability, string? path, HttpContext context)
    {
        if (!access.Matches(key))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var target = destinations.Resolve(capability);
        if (target is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var remaining = string.IsNullOrWhiteSpace(path) ? "/" : "/" + path.TrimStart('/');
        if (context.Request.QueryString.HasValue)
            remaining += context.Request.QueryString.Value;
        if (!Uri.TryCreate(target.Origin.TrimEnd('/') + remaining, UriKind.Absolute, out var destination))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        try
        {
            await forwarder.ForwardAsync(
                context,
                destination,
                target.BearerToken,
                replaceIncomingAuthorization: string.Equals(capability, "opencode", StringComparison.OrdinalIgnoreCase));
        }
        catch (HttpRequestException)
        {
            WriteUnavailable(context);
        }
        catch (IOException)
        {
            WriteUnavailable(context);
        }
        catch (TaskCanceledException) when (!context.RequestAborted.IsCancellationRequested)
        {
            WriteUnavailable(context);
        }
    }

    internal static void WriteUnavailable(HttpContext context)
    {
        if (context.Response.HasStarted)
        {
            context.Abort();
            return;
        }

        context.Response.StatusCode = StatusCodes.Status502BadGateway;
        context.Response.Headers.CacheControl = "no-store";
    }
}

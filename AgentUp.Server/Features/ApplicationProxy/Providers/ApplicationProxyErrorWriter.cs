using System.Text.Json;
using AgentUp.Server.Features.ApplicationProxy.Interfaces;
using AgentUp.Server.Features.ApplicationProxy.Models;

namespace AgentUp.Server.Features.ApplicationProxy.Providers;

public sealed class ApplicationProxyErrorWriter : IApplicationProxyErrorWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(HttpContext context, ApplicationProxyAccessResult result)
    {
        context.Response.StatusCode = result.StatusCode;
        context.Response.Headers.CacheControl = "no-store";
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            title = result.Title,
            detail = result.Detail,
            status = result.StatusCode
        }, Json));
    }
}

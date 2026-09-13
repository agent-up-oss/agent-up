namespace AgentUp.Server.Features.ApplicationProxy.Models;

public sealed class ApplicationProxyAccessResult
{
    public int StatusCode { get; init; } = StatusCodes.Status200OK;
    public string? Title { get; init; }
    public string? Detail { get; init; }
    public ApplicationProxySession? Session { get; init; }
    public bool ConsumedTicket { get; init; }
}

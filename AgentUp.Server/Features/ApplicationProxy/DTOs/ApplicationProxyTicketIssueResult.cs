namespace AgentUp.Server.Features.ApplicationProxy.DTOs;

public sealed class ApplicationProxyTicketIssueResult
{
    public ApplicationProxyTicketResponse? Response { get; init; }
    public int StatusCode { get; init; } = StatusCodes.Status200OK;
    public string? Title { get; init; }
    public string? Detail { get; init; }
}

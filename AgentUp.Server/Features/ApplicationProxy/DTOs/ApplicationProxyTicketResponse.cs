namespace AgentUp.Server.Features.ApplicationProxy.DTOs;

public sealed record ApplicationProxyTicketResponse(
    string Ticket,
    string BootstrapPath,
    DateTimeOffset ExpiresAt);

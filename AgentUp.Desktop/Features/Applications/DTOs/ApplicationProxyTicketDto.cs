namespace AgentUp.Desktop.Features.Applications.DTOs;

public sealed record ApplicationProxyTicketDto(
    string Ticket,
    string BootstrapPath,
    DateTimeOffset ExpiresAt);

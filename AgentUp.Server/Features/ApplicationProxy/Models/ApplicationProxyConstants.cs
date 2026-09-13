namespace AgentUp.Server.Features.ApplicationProxy.Models;

public static class ApplicationProxyConstants
{
    public const string CookieName = "agent-up-proxy";
    public const string TicketQuery = "ticket";
    public const string TicketHeader = "X-Agent-Up-Ticket";
    public const string DestinationPortItem = "AgentUp.ApplicationProxy.Port";
    public static readonly TimeSpan TicketLifetime = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);
    public static readonly string[] ReservedFallbackPrefixes = ["/api", "/mcp", "/apps"];
}

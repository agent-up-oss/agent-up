using AgentUp.Server.Features.ApplicationProxy.DTOs;
using AgentUp.Server.Features.ApplicationProxy.Interfaces;
using AgentUp.Server.Features.ApplicationProxy.Models;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;

namespace AgentUp.Server.Features.ApplicationProxy.Services;

public sealed class ApplicationProxyService(
    WorkspaceQueryController workspaces,
    ILoopbackHttpPortProbe probe,
    IApplicationProxyTicketStore tickets,
    IApplicationProxyCredentials credentials,
    IApplicationProxyOriginMapper origin,
    IApplicationProxyCsrfGuard csrf,
    IApplicationHttpForwarder forwarder,
    IApplicationProxyErrorWriter errors,
    IApplicationProxyTransportGuard transport,
    IApplicationProxyBootstrapPage bootstrap,
    TimeProvider clock)
{
    public ApplicationProxyTicketIssueResult IssueTicket(ApplicationProxyTicketRequest request, HttpContext context)
    {
        if (!transport.AllowsCredentials(context))
            return DenyIssue(StatusCodes.Status400BadRequest, "HTTPS required", "Application proxy credentials require HTTPS except on loopback development URLs.");

        var access = ValidatePort(request.WorkspaceId, request.AllocatedPort);
        if (access.Session is null)
        {
            return new ApplicationProxyTicketIssueResult
            {
                StatusCode = access.StatusCode,
                Title = access.Title,
                Detail = access.Detail
            };
        }

        var session = new ApplicationProxySession
        {
            WorkspaceId = access.Session.WorkspaceId,
            AllocatedPort = access.Session.AllocatedPort,
            ExpiresAt = clock.GetUtcNow().Add(ApplicationProxyConstants.TicketLifetime)
        };
        var ticket = tickets.Issue(session);
        return new ApplicationProxyTicketIssueResult
        {
            Response = new ApplicationProxyTicketResponse(
                ticket,
                $"/apps/{Uri.EscapeDataString(session.WorkspaceId)}/{session.AllocatedPort}",
                session.ExpiresAt)
        };
    }

    public async Task OpenAsync(string workspaceId, int port, string? path, HttpContext context)
    {
        if (!transport.AllowsCredentials(context))
        {
            await errors.WriteAsync(context, Denied(StatusCodes.Status400BadRequest, "HTTPS required", "Application proxy credentials require HTTPS except on loopback development URLs."));
            return;
        }

        var access = Authorize(workspaceId, port, context);
        if (access.Session is null)
        {
            if (ShouldWriteBootstrap(context, access.StatusCode))
            {
                await bootstrap.WriteAsync(context);
                return;
            }

            await errors.WriteAsync(context, access);
            return;
        }

        await SendToApplicationAsync(context, access.Session, path ?? string.Empty, access.ConsumedTicket);
    }

    public async Task ForwardFallbackAsync(HttpContext context)
    {
        if (!transport.AllowsCredentials(context))
        {
            await errors.WriteAsync(context, Denied(StatusCodes.Status400BadRequest, "HTTPS required", "Application proxy credentials require HTTPS except on loopback development URLs."));
            return;
        }

        if (origin.IsReservedFallbackPath(context))
        {
            await errors.WriteAsync(context, Denied(StatusCodes.Status404NotFound, "Not Found", "Reserved Server routes are not proxied to workspace applications."));
            return;
        }

        var session = credentials.ReadSession(context, clock.GetUtcNow());
        if (session is null)
        {
            await errors.WriteAsync(context, Denied(StatusCodes.Status404NotFound, "Not Found", "No application proxy session is active."));
            return;
        }

        var access = ValidatePort(session.WorkspaceId, session.AllocatedPort);
        if (access.Session is null)
        {
            await errors.WriteAsync(context, access);
            return;
        }

        if (csrf.IsForeignOrigin(context))
        {
            await errors.WriteAsync(context, Denied(StatusCodes.Status403Forbidden, "Forbidden", "Cross-origin application proxy writes are not allowed."));
            return;
        }

        await forwarder.ForwardAsync(context, session.AllocatedPort);
    }

    private async Task SendToApplicationAsync(HttpContext context, ApplicationProxySession session, string path, bool consumedTicket)
    {
        credentials.WriteSession(context, new ApplicationProxySession
        {
            WorkspaceId = session.WorkspaceId,
            AllocatedPort = session.AllocatedPort,
            ExpiresAt = clock.GetUtcNow().Add(ApplicationProxyConstants.SessionLifetime)
        });
        credentials.StripTicketFromQuery(context);

        if (consumedTicket || origin.ShouldRedirectToOrigin(context))
        {
            await origin.RedirectToOriginRootAsync(context);
            return;
        }

        origin.ApplyApplicationPath(context, path);
        await forwarder.ForwardAsync(context, session.AllocatedPort);
    }

    private ApplicationProxyAccessResult Authorize(string workspaceId, int port, HttpContext context)
    {
        var access = ValidatePort(workspaceId, port);
        if (access.Session is null)
            return access;

        if (csrf.IsForeignOrigin(context))
            return Denied(StatusCodes.Status403Forbidden, "Forbidden", "Cross-origin application proxy writes are not allowed.");

        var now = clock.GetUtcNow();
        var ticket = credentials.ReadTicket(context);
        if (ticket is not null)
            return ConsumeTicket(ticket, workspaceId, port, now);

        if (context.User.Identity?.IsAuthenticated == true)
            return access;

        var cookie = credentials.ReadSession(context, now);
        if (cookie is not null
            && string.Equals(cookie.WorkspaceId, workspaceId, StringComparison.Ordinal)
            && cookie.AllocatedPort == port)
        {
            return access;
        }

        return Denied(StatusCodes.Status401Unauthorized, "Unauthorized", "An application proxy ticket or authenticated session is required.");
    }

    private ApplicationProxyAccessResult ConsumeTicket(string ticket, string workspaceId, int port, DateTimeOffset now)
    {
        var session = tickets.Consume(ticket, now);
        if (session is null)
            return Denied(StatusCodes.Status401Unauthorized, "Unauthorized", "The application proxy ticket is invalid or has already been used.");
        if (!string.Equals(session.WorkspaceId, workspaceId, StringComparison.Ordinal) || session.AllocatedPort != port)
            return Denied(StatusCodes.Status403Forbidden, "Forbidden", "The application proxy ticket does not match this application port.");
        return new ApplicationProxyAccessResult { Session = session, ConsumedTicket = true };
    }

    private ApplicationProxyAccessResult ValidatePort(string workspaceId, int port)
    {
        if (port is < 1 or > 65535)
            return Denied(StatusCodes.Status400BadRequest, "Invalid port", "Allocated ports must be between 1 and 65535.");

        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return Denied(StatusCodes.Status404NotFound, "Workspace not found", "The workspace does not exist.");

        var mapping = FindHttpPort(workspace, port);
        if (mapping is null)
            return Denied(StatusCodes.Status404NotFound, "Port not found", "That port is not an allocated HTTP port of this workspace.");

        if (!probe.IsListening(port))
            return Denied(StatusCodes.Status503ServiceUnavailable, "Port closed", "That application port is not currently open.");

        return new ApplicationProxyAccessResult
        {
            Session = new ApplicationProxySession
            {
                WorkspaceId = workspace.Id,
                AllocatedPort = mapping.AllocatedPort,
                ExpiresAt = clock.GetUtcNow().Add(ApplicationProxyConstants.SessionLifetime)
            }
        };
    }

    private static bool ShouldWriteBootstrap(HttpContext context, int statusCode)
        => statusCode == StatusCodes.Status401Unauthorized
           && HttpMethods.IsGet(context.Request.Method)
           && string.IsNullOrWhiteSpace(context.Request.Headers[ApplicationProxyConstants.TicketHeader]);

    private static PortMapping? FindHttpPort(Workspace workspace, int port)
        => workspace.Applications
            .SelectMany(application => application.AllocatedPorts)
            .FirstOrDefault(mapping => mapping.AllocatedPort == port
                                       && string.Equals(mapping.Protocol, "http", StringComparison.OrdinalIgnoreCase));

    private static ApplicationProxyAccessResult Denied(int status, string title, string detail)
        => new() { StatusCode = status, Title = title, Detail = detail };

    private static ApplicationProxyTicketIssueResult DenyIssue(int status, string title, string detail)
        => new() { StatusCode = status, Title = title, Detail = detail };
}

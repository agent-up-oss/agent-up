using System.Net.Http.Json;
using AgentUp.Desktop.Features.Applications.DTOs;

namespace AgentUp.Desktop.Features.Applications.Providers;

public sealed class ApplicationProxyClient(HttpClient http, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<Uri> IssueNavigationUriAsync(
        Uri serverUri,
        string workspaceId,
        int allocatedPort,
        string? destinationPathAndQuery = null,
        CancellationToken cancellationToken = default)
    {
        // A ticket can expire while the request is in flight. Reissue once rather than
        // sending a WebView to a bootstrap page with credentials that are already stale.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await http.PostAsJsonAsync(
                new Uri(serverUri, "/api/apps/tickets"),
                new { workspaceId, allocatedPort },
                cancellationToken);
            response.EnsureSuccessStatusCode();
            var ticket = await response.Content.ReadFromJsonAsync<ApplicationProxyTicketDto>(cancellationToken);
            if (ticket is null || string.IsNullOrWhiteSpace(ticket.Ticket) || string.IsNullOrWhiteSpace(ticket.BootstrapPath))
                throw new InvalidDataException("The Server did not issue an application proxy ticket.");

            if (ticket.ExpiresAt <= _clock.GetUtcNow())
                continue;

            var bootstrapUri = new Uri(serverUri, ticket.BootstrapPath);
            var fragment = $"ticket={Uri.EscapeDataString(ticket.Ticket)}";
            if (!string.IsNullOrWhiteSpace(destinationPathAndQuery)
                && destinationPathAndQuery != "/")
            {
                fragment += $"&return={Uri.EscapeDataString(destinationPathAndQuery)}";
            }

            return new UriBuilder(bootstrapUri)
            {
                Fragment = fragment
            }.Uri;
        }

        throw new InvalidDataException("The Server issued an expired application proxy ticket.");
    }
}

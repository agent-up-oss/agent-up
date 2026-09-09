using System.Net;
using System.Net.Http.Json;
using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Ports.DTOs;
using AgentUp.Desktop.Features.Workspaces.DTOs;

namespace AgentUp.Tests.Support;

// Stands in for AgentUp.Server so the Desktop end-to-end tests exercise the real window,
// the real WebView, and a real application HTTP server without needing a Server process.
// Only the workspace list matters here: it carries the allocated HTTP port that the Desktop
// browser tab navigates to. Everything else answers 204 so the periodic Desktop pollers
// (host metrics, audit, console) stay quiet instead of failing against a dead port.
internal sealed class DesktopServerStub(string workspaceId, int applicationPort) : HttpMessageHandler
{
    internal const string ApplicationName = "e2e-app";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri?.AbsolutePath == "/api/workspaces")
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new List<WorkspaceDto> { Workspace() })
            });

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));
    }

    private WorkspaceDto Workspace()
        => new(
            workspaceId,
            workspaceId,
            $"/repo/{workspaceId}",
            $"/worktrees/{workspaceId}",
            "main",
            "e2ec0de",
            "Running")
        {
            Applications =
            [
                new ApplicationDto(ApplicationName, "serve", null, "Running")
                {
                    AllocatedPorts = [new PortMappingDto("PORT", applicationPort, applicationPort)]
                }
            ]
        };
}

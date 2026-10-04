using System.Security.Claims;
using AgentUp.Server.Features.Authentication.Interfaces;
using AgentUp.Server.Features.Authentication.Services;
using ModelContextProtocol.Server;

namespace AgentUp.Server.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class McpWorkspaceBindingServiceTests
{
    [Test]
    public void Pin_AttachesFiltersWhenThePrincipalHasAWorkspaceClaim()
    {
        var filters = new RecordingFilters();
        var service = new McpWorkspaceBindingService(filters);
        var options = new McpServerOptions();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("workspace", "ws-a")]));

        service.Pin(user, options);

        Assert.That(filters.BoundWorkspace, Is.EqualTo("ws-a"));
    }

    [Test]
    public void Pin_LeavesOptionsUnchangedWhenThePrincipalIsUnbound()
    {
        var filters = new RecordingFilters();
        var service = new McpWorkspaceBindingService(filters);

        service.Pin(new ClaimsPrincipal(new ClaimsIdentity()), new McpServerOptions());

        Assert.That(filters.BoundWorkspace, Is.Null);
    }

    private sealed class RecordingFilters : IMcpWorkspaceBindingFilterProvider
    {
        public string? BoundWorkspace { get; private set; }

        public void Attach(McpServerOptions options, string boundWorkspace)
            => BoundWorkspace = boundWorkspace;
    }
}

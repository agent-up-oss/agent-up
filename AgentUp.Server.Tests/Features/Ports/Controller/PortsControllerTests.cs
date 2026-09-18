using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Ports.Interfaces;

namespace AgentUp.Server.Tests.Features.Ports.Controller;

[TestFixture]
public sealed class PortsControllerTests
{
    [Test]
    public async Task GetBasePortAsync_returns_the_service_result()
    {
        var service = new RecordingPortAllocationService { BasePort = 12300 };
        var controller = new PortsController(service);

        var result = await controller.GetBasePortAsync("workspace-a");

        Assert.That(result, Is.EqualTo(12300));
        Assert.That(service.WorkspaceId, Is.EqualTo("workspace-a"));
    }

    [Test]
    public async Task GetConflictFreeBasePortAsync_forwards_the_requested_port_count()
    {
        var service = new RecordingPortAllocationService { ConflictFreeBasePort = 14500 };
        var controller = new PortsController(service);

        var result = await controller.GetConflictFreeBasePortAsync("workspace-b", 7);

        Assert.That(result, Is.EqualTo(14500));
        Assert.Multiple(() =>
        {
            Assert.That(service.WorkspaceId, Is.EqualTo("workspace-b"));
            Assert.That(service.PortCount, Is.EqualTo(7));
        });
    }

    [Test]
    public async Task ReleaseAsync_forwards_the_workspace_id()
    {
        var service = new RecordingPortAllocationService();
        var controller = new PortsController(service);

        await controller.ReleaseAsync("workspace-c");

        Assert.That(service.ReleasedWorkspaceId, Is.EqualTo("workspace-c"));
    }

    private sealed class RecordingPortAllocationService : IPortAllocationService
    {
        public int BasePort { get; init; }
        public int ConflictFreeBasePort { get; init; }
        public string? WorkspaceId { get; private set; }
        public string? ReleasedWorkspaceId { get; private set; }
        public int PortCount { get; private set; }

        public Task<int> GetBasePortAsync(string workspaceId)
        {
            WorkspaceId = workspaceId;
            return Task.FromResult(BasePort);
        }

        public Task<int> GetConflictFreeBasePortAsync(string workspaceId, int portCount)
        {
            WorkspaceId = workspaceId;
            PortCount = portCount;
            return Task.FromResult(ConflictFreeBasePort);
        }

        public Task ReleaseAsync(string workspaceId)
        {
            ReleasedWorkspaceId = workspaceId;
            return Task.CompletedTask;
        }
    }
}

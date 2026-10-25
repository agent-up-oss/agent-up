using AgentUp.Server.Features.Agents.Models;
using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Agents.Services;
using AgentUp.Server.Features.Capabilities.Controllers;
using AgentUp.Server.Features.Capabilities.Services;
using AgentUp.Server.Features.Ports.Controllers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Features.Agents.Unit;
using AgentUp.Server.Tests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentSchedulingServiceAvailabilityTests
{
    [Test]
    public async Task GetAsync_namesBothDevelopmentAgentsWhenTheHostIsDevelopment()
    {
        var (workspace, service) = await CreateAsync(new AgentCommandProvider(new ConfigurationBuilder().Build()));
        try
        {
            var session = await service.GetAsync(workspace.Id, CancellationToken.None);
            var listed = session!.Agents.ToDictionary(agent => agent.Agent, StringComparer.Ordinal);

            Assert.Multiple(() =>
            {
                Assert.That(listed[AgentUnavailability.ScriptedTestAgent].DisplayName, Is.EqualTo(AgentUnavailability.ScriptedTestAgentDisplayName));
                Assert.That(listed[AgentUnavailability.LocalInferenceTestAgent].DisplayName, Is.EqualTo(AgentUnavailability.LocalInferenceTestAgentDisplayName));
            });
        }
        finally
        {
            await service.DisposeAsync();
        }
    }

    [Test]
    public async Task GetAsync_listsLocalInferenceTestAgentWhenTheHostIsDevelopment()
    {
        var commands = new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Agents:test-agent-infer:Command"] = $"/definitely-missing/{AgentUnavailability.LocalInferenceTestAgent}"
            }).Build());
        var (workspace, service) = await CreateAsync(commands);
        try
        {
            var session = await service.GetAsync(workspace.Id, CancellationToken.None);
            var infer = session!.Agents.Single(agent => agent.Agent == AgentUnavailability.LocalInferenceTestAgent);

            Assert.Multiple(() =>
            {
                Assert.That(infer.DisplayName, Is.EqualTo(AgentUnavailability.LocalInferenceTestAgentDisplayName));
                Assert.That(infer.Available, Is.False);
                Assert.That(infer.Unavailability, Is.EqualTo(AgentUnavailability.ExecutableNotFound));
                Assert.That(infer.Remedy, Does.Contain("test-agent-infer"));
            });
        }
        finally
        {
            await service.DisposeAsync();
        }
    }

    [Test]
    public async Task GetAsync_marksLocalInferenceTestAgentAvailableWhenItsCommandExists()
    {
        var command = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh";
        var commands = new AgentCommandProvider(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Agents:test-agent-infer:Command"] = command }).Build());
        var (workspace, service) = await CreateAsync(commands);
        try
        {
            var session = await service.GetAsync(workspace.Id, CancellationToken.None);
            var infer = session!.Agents.Single(agent => agent.Agent == AgentUnavailability.LocalInferenceTestAgent);

            Assert.That(infer.Available, Is.True);
            Assert.That(infer.Unavailability, Is.Null);
        }
        finally
        {
            await service.DisposeAsync();
        }
    }

    private static async Task<(Workspace Workspace, AgentSchedulingService Service)> CreateAsync(
        AgentCommandProvider commands)
    {
        var registry = new WorkspaceRegistry(
            new InMemoryWorkspaceRepository(),
            new PortsController(new InMemoryPortAllocationService()),
            new CapabilitiesController(new CapabilityReconciliationService()),
            new WorkspaceEventBus());
        await registry.StartAsync(CancellationToken.None);
        var workspace = await registry.RegisterAsync(ServerDomain.Workspace().Named("Workspace").At("/repo").AtCommit("abc").Build());
        var process = new FakeAgentProcessProvider();
        var payloads = new AgentEventFrameProvider();
        var service = new AgentSchedulingService(
            new WorkspaceQueryController(registry),
            new FakeAgentProcessFactory(process),
            commands,
            new FakeSubscriptionLoginProvider(),
            new FakeProcessEnvironmentProvider(),
            new FakeClaudeCredentialStore(),
            new AgentSubscriptionAuth(),
            payloads,
            new AgentEventService(payloads),
            NullLogger<AgentSchedulingService>.Instance,
            host: new StubHostEnvironment(Environments.Development));
        return (workspace, service);
    }
}

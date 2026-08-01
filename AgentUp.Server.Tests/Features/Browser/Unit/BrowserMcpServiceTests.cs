using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Browser.Models;
using AgentUp.Server.Features.Browser.Services;
using AgentUp.Server.Features.Ports.DTOs;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Services;
using AgentUp.Server.Tests.Fake;
using System.Text.Json;

namespace AgentUp.Server.Tests.Features.Browser.Unit;

[TestFixture]
public sealed class BrowserMcpServiceTests
{
    [Test]
    public async Task ClickAsync_ReturnsBrowserStateData()
    {
        var store = new BrowserSessionStore();
        var events = new InMemoryAuditEventRepository();
        var service = Service(store, events: events);
        var state = "{\"url\":\"http://localhost:3000\",\"interactive\":[]}";
        var resultTask = service.ClickAsync("workspace", "#save", CancellationToken.None);

        var command = await store.TryDequeueAsync(
            ["workspace"],
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        Assert.That(command, Is.Not.Null);

        store.CompleteCommand(new BrowserCommandResultDto(command!.CommandId, true, state, null));

        var result = await resultTask;

        Assert.That(result.Succeeded, Is.True);
        Assert.That(result.Data, Is.EqualTo(state));
        var evt = events.Events.Single();
        Assert.Multiple(() =>
        {
            Assert.That(evt.Action, Is.EqualTo("browser_click"));
            Assert.That(evt.Details["pageUrl"], Is.EqualTo("http://localhost:3000"));
            Assert.That(evt.Details["interactiveCount"], Is.EqualTo("0"));
            Assert.That(evt.Details.ContainsKey("data"), Is.False);
        });
    }

    [Test]
    public async Task WaitForSelectorAsync_UsesCallerTimeoutForDispatchedCommand()
    {
        var store = new BrowserSessionStore();
        var service = Service(store);
        var resultTask = service.WaitForSelectorAsync("workspace", "#ready", 60_000, CancellationToken.None);

        var command = await store.TryDequeueAsync(
            ["workspace"],
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        Assert.That(command, Is.Not.Null);
        Assert.That(command!.TimeoutMs, Is.EqualTo(60_000));

        store.CompleteCommand(new BrowserCommandResultDto(command.CommandId, true, "{}", null));
        var result = await resultTask;

        Assert.That(result.Succeeded, Is.True);
    }

    [Test]
    public async Task ScreenshotAsync_ReturnsInlineImageAndAuditArtifact()
    {
        var store = new BrowserSessionStore();
        var events = new InMemoryAuditEventRepository();
        var service = Service(store, events: events);
        var image = Convert.ToBase64String([1, 2, 3]);
        var resultTask = service.ScreenshotAsync("workspace", CancellationToken.None);

        var command = await store.TryDequeueAsync(
            ["workspace"],
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        Assert.That(command, Is.Not.Null);
        var screenshot = new BrowserScreenshotResultDto(
            "http://localhost:3000",
            "image/png",
            image,
            1280,
            720);

        store.CompleteCommand(new BrowserCommandResultDto(
            command!.CommandId,
            true,
            JsonSerializer.Serialize(screenshot),
            null));

        var result = await resultTask;
        var data = JsonSerializer.SerializeToElement(result.Data);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(data.GetProperty("imageBase64").GetString(), Is.EqualTo(image));
            Assert.That(data.GetProperty("artifactId").GetString(), Is.Not.Null.And.Not.Empty);
            Assert.That(events.Events.Single().Action, Is.EqualTo("browser_screenshot"));
        });
    }

    [Test]
    public async Task ScreenshotAsync_RejectsOversizedInlineImage()
    {
        var store = new BrowserSessionStore();
        var events = new InMemoryAuditEventRepository();
        var service = Service(store, events: events);
        var image = Convert.ToBase64String(new byte[(220 * 1024) + 1]);
        var resultTask = service.ScreenshotAsync("workspace", CancellationToken.None);

        var command = await store.TryDequeueAsync(
            ["workspace"],
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        Assert.That(command, Is.Not.Null);
        store.CompleteCommand(new BrowserCommandResultDto(
            command!.CommandId,
            true,
            JsonSerializer.Serialize(new BrowserScreenshotResultDto(
                "http://localhost:3000",
                "image/png",
                image,
                1280,
                720)),
            null));

        var result = await resultTask;

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("inline image was larger"));
            Assert.That(events.Events.Single().Outcome, Is.EqualTo("failure"));
        });
    }

    [Test]
    public async Task NavigateAsync_BlocksUrlOutsideWorkspaceHttpPorts()
    {
        var store = new BrowserSessionStore();
        var events = new InMemoryAuditEventRepository();
        var (registry, workspaceId) = await RegistryWithWorkspaceAsync();
        var service = Service(store, events, registry);

        var result = await service.NavigateAsync(workspaceId, "https://example.com", CancellationToken.None);
        var dequeued = await store.TryDequeueAsync([workspaceId], TimeSpan.FromMilliseconds(100), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("only this workspace's allocated local application ports"));
            Assert.That(dequeued, Is.Null);
            Assert.That(events.Events.Single().Outcome, Is.EqualTo("failure"));
        });
    }

    [Test]
    public async Task NavigateAsync_AllowsWorkspaceAllocatedHttpPort()
    {
        var store = new BrowserSessionStore();
        var (registry, workspaceId) = await RegistryWithWorkspaceAsync();
        var port = registry.GetById(workspaceId)!.Applications.Single().AllocatedPorts.Single().AllocatedPort;
        var service = Service(store, registry: registry);
        var resultTask = service.NavigateAsync(workspaceId, $"http://localhost:{port}/path", CancellationToken.None);

        var command = await store.TryDequeueAsync(
            [workspaceId],
            TimeSpan.FromSeconds(1),
            CancellationToken.None);
        Assert.That(command, Is.Not.Null);
        store.CompleteCommand(new BrowserCommandResultDto(command!.CommandId, true, "{}", null));

        var result = await resultTask;

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.True);
            Assert.That(command.Url, Is.EqualTo($"http://localhost:{port}/path"));
        });
    }

    private static BrowserMcpService Service(
        BrowserSessionStore store,
        InMemoryAuditEventRepository? events = null,
        WorkspaceRegistry? registry = null)
    {
        registry ??= ServerTestComposition.CreateRegistry();
        return new BrowserMcpService(
            store,
            ServerTestComposition.CreateAuditController(registry, events: events),
            new WorkspaceQueryController(registry));
    }

    private static async Task<(WorkspaceRegistry Registry, string WorkspaceId)> RegistryWithWorkspaceAsync()
    {
        var registry = ServerTestComposition.CreateRegistry();
        var workspace = await registry.RegisterAsync(new RegisterWorkspaceRequest(
            "Workspace",
            "/repo",
            "/repo/worktree",
            "main",
            "abc")
        {
            Applications =
            [
                new ApplicationDefinition(
                    "Web",
                    "npm run start",
                    "web",
                    [new PortDeclaration("PORT", 3000, "http")])
            ]
        });
        return (registry, workspace.Id);
    }
}

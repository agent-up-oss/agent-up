using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.DTOs;
using AgentUp.Desktop.Features.FakeServer.Models;

namespace AgentUp.Desktop.Features.FakeServer.Services;

public sealed class FakeBackendService
{
    private readonly FakeServerDefinition _template;
    private readonly Lock _gate = new();
    private FakeServerDefinition _state;
    private readonly Dictionary<string, List<FakeServerEvent>> _agentEvents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Action<FakeServerEvent>>> _agentListeners = new(StringComparer.Ordinal);
    private readonly List<Action<string>> _workspaceListeners = [];
    private long _agentSequence;

    public FakeBackendService(FakeServerDefinition definition)
    {
        _template = definition;
        _state = definition.Clone();
    }

    public FakeServerConnectionDto Catalog(string? activeServerId)
        => new(FakeServerIdentity.Id, FakeServerIdentity.Url, FakeServerIdentity.DisplayName,
            string.Equals(activeServerId, FakeServerIdentity.Id, StringComparison.Ordinal));

    public bool Matches(Uri? uri) => FakeServerIdentity.Matches(uri);

    public bool Matches(string? url) => FakeServerIdentity.Matches(url);

    public void Reset()
    {
        lock (_gate)
        {
            _state = _template.Clone();
            _agentEvents.Clear();
            _agentSequence = 0;
        }
    }

    public string? ApplicationHtml(int allocatedPort)
    {
        lock (_gate)
        {
            var page = FindPageName(allocatedPort);
            return page is null ? null : _state.Pages?[page]?.GetValue<string>();
        }
    }

    public string? ApplicationHtml(string page)
    {
        lock (_gate)
            return _state.Pages?[page]?.GetValue<string>();
    }

    public FakeBackendResponseDto Handle(FakeBackendRequestDto request)
    {
        var method = request.Method.ToUpperInvariant();
        var path = request.Path.TrimEnd('/');
        if (path.Length == 0)
            path = "/";

        if (method == "GET" && path == "/api/auth/status")
            return Json(_state.Authentication);
        if (method == "POST" && path == "/api/auth/login")
            return Json(new JsonObject { ["authenticationRequired"] = false, ["accessToken"] = "fake-token" });
        if (method == "GET" && path == "/api/connection")
            return Json(_state.Connection);
        if (method == "GET" && path == "/api/entitlements")
            return Json(_state.Entitlements);
        if (method == "GET" && path == "/api/workspaces")
            return Json(_state.Workspaces);
        if (method == "GET" && path == "/api/workspaces/events")
            return new FakeBackendResponseDto(200, "text/event-stream", WorkspaceSnapshotSse(), KeepOpen: true);
        if (method == "POST" && path == "/api/workspaces/tutorial/cleanup")
            return new FakeBackendResponseDto(204, "application/json");
        if (method == "POST" && path == "/api/source-clones")
            return CloneWorkspace(request.Body);
        if (method == "POST" && path == "/api/apps/tickets")
            return IssueTicket(request.Body);
        if (method == "POST" && path == "/api/audit/record")
            return new FakeBackendResponseDto(204, "application/json");
        if (method == "GET" && path.StartsWith("/apps/", StringComparison.Ordinal))
            return AppPage(path);

        if (TryWorkspacePath(path, out var workspaceId, out var rest))
            return HandleWorkspace(method, workspaceId, rest, request);

        return NotFound();
    }

    public IReadOnlyList<FakeServerEvent> AgentEventsAfter(string workspaceId, long after)
    {
        lock (_gate)
        {
            if (!_agentEvents.TryGetValue(workspaceId, out var events))
                return [];
            return events.Where(item => item.Sequence > after).Select(CloneEvent).ToArray();
        }
    }

    public IDisposable SubscribeAgent(string workspaceId, Action<FakeServerEvent> listener)
    {
        lock (_gate)
        {
            if (!_agentListeners.TryGetValue(workspaceId, out var listeners))
            {
                listeners = [];
                _agentListeners[workspaceId] = listeners;
            }

            listeners.Add(listener);
        }

        return new FakeServerSubscription(() => RemoveAgentListener(workspaceId, listener));
    }

    public IDisposable SubscribeWorkspaces(Action<string> listener)
    {
        lock (_gate)
            _workspaceListeners.Add(listener);
        return new FakeServerSubscription(() => RemoveWorkspaceListener(listener));
    }

    private void RemoveAgentListener(string workspaceId, Action<FakeServerEvent> listener)
    {
        lock (_gate)
        {
            if (_agentListeners.TryGetValue(workspaceId, out var listeners))
                listeners.Remove(listener);
        }
    }

    private void RemoveWorkspaceListener(Action<string> listener)
    {
        lock (_gate)
            _workspaceListeners.Remove(listener);
    }

    public string WorkspaceSnapshotSse()
    {
        lock (_gate)
            return string.Concat(_state.Workspaces.Select(WorkspaceEventFrame));
    }

    private FakeBackendResponseDto HandleWorkspace(string method, string workspaceId, string rest, FakeBackendRequestDto request)
    {
        if (method == "GET" && rest.Length == 0)
            return WorkspaceJson(workspaceId);
        if (method == "DELETE" && rest.Length == 0)
            return DeleteWorkspace(workspaceId);
        if (method == "POST" && rest == "start")
            return SetWorkspaceState(workspaceId, "Running");
        if (method == "POST" && rest == "stop")
            return SetWorkspaceState(workspaceId, "Stopped");
        if (method == "GET" && rest == "overview")
            return Overview(workspaceId);
        if (method == "GET" && rest == "git/changes")
            return GitNode(workspaceId, "changes");
        if (method == "GET" && rest == "git/log")
            return GitNode(workspaceId, "log");
        if (method == "GET" && rest == "commit-queue")
            return GitNode(workspaceId, "queue");
        if (method == "GET" && rest.StartsWith("git/file", StringComparison.Ordinal))
            return GitDiff(workspaceId, request.Query);
        if (method == "POST" && rest.StartsWith("git/", StringComparison.Ordinal))
            return GitMutation(workspaceId, rest["git/".Length..]);
        if (method == "GET" && rest == "agent")
            return AgentSession(workspaceId);
        if (method == "POST" && rest == "agent")
            return AgentSession(workspaceId);
        if (method == "POST" && rest == "agent/messages")
            return SendAgentMessage(workspaceId, request.Body);
        if (method == "GET" && rest == "agent/events")
            return AgentEventStream(workspaceId, request.Query);
        if (method is "POST" or "DELETE" && rest.StartsWith("agent/", StringComparison.Ordinal))
            return new FakeBackendResponseDto(204, "application/json");
        if (method == "DELETE" && rest == "agent")
            return new FakeBackendResponseDto(204, "application/json");
        if (method == "GET" && rest.EndsWith("/output", StringComparison.Ordinal))
            return ConsoleOutput(workspaceId, rest);
        if (method == "GET" && rest.EndsWith("/metrics", StringComparison.Ordinal))
            return Json(new JsonArray());
        if (method == "GET" && rest.EndsWith("/validation-flows", StringComparison.Ordinal))
            return Json(new JsonArray());
        if (method == "POST" && rest.Contains("/viewer-ticket", StringComparison.Ordinal))
            return ViewerTicket(workspaceId, rest);
        if (method == "GET" && rest.Contains("/database/", StringComparison.Ordinal))
            return Json(new JsonArray());
        if (method == "POST" && rest.Contains("/database/", StringComparison.Ordinal))
            return Json(new JsonObject { ["rows"] = new JsonArray(), ["error"] = null });

        return NotFound();
    }

    private FakeBackendResponseDto WorkspaceJson(string workspaceId)
    {
        var workspace = FindWorkspace(workspaceId);
        return workspace is null ? NotFound() : Json(workspace);
    }

    private FakeBackendResponseDto DeleteWorkspace(string workspaceId)
    {
        lock (_gate)
        {
            for (var index = _state.Workspaces.Count - 1; index >= 0; index--)
            {
                if (string.Equals(_state.Workspaces[index]?["id"]?.GetValue<string>(), workspaceId, StringComparison.Ordinal))
                    _state.Workspaces.RemoveAt(index);
            }
        }

        PublishWorkspaces();
        return new FakeBackendResponseDto(204, "application/json");
    }

    private FakeBackendResponseDto SetWorkspaceState(string workspaceId, string state)
    {
        lock (_gate)
        {
            var workspace = FindWorkspace(workspaceId);
            if (workspace is null)
                return NotFound();
            workspace["state"] = state;
            if (workspace["applications"] is JsonArray applications)
            {
                foreach (var application in applications)
                {
                    if (application is JsonObject app)
                        app["state"] = state == "Running" ? "Running" : "Stopped";
                }
            }
        }

        PublishWorkspaces();
        return new FakeBackendResponseDto(204, "application/json");
    }

    private FakeBackendResponseDto Overview(string workspaceId)
    {
        var workspace = FindWorkspace(workspaceId);
        if (workspace is null)
            return NotFound();

        var overview = _state.Overview?[workspaceId] as JsonObject ?? new JsonObject();
        var applications = workspace["applications"] as JsonArray;
        var payload = new JsonObject
        {
            ["id"] = workspace["id"]?.DeepClone(),
            ["displayName"] = workspace["displayName"]?.DeepClone(),
            ["repositoryPath"] = workspace["repositoryPath"]?.DeepClone(),
            ["worktreePath"] = workspace["worktreePath"]?.DeepClone(),
            ["branch"] = workspace["branch"]?.DeepClone(),
            ["commit"] = workspace["commit"]?.DeepClone(),
            ["state"] = workspace["state"]?.DeepClone(),
            ["cpuPercent"] = overview["cpuPercent"]?.DeepClone() ?? 0,
            ["memoryBytes"] = overview["memoryBytes"]?.DeepClone() ?? 0,
            ["storageBytes"] = overview["storageBytes"]?.DeepClone() ?? 0,
            ["processCount"] = overview["processCount"]?.DeepClone() ?? 0,
            ["applicationCount"] = applications?.Count ?? 0
        };
        return Json(payload);
    }

    private FakeBackendResponseDto GitNode(string workspaceId, string name)
    {
        var node = _state.Git?[workspaceId]?[name];
        return node is null ? NotFound() : Json(node);
    }

    private FakeBackendResponseDto GitDiff(string workspaceId, string query)
    {
        var path = QueryValue(query, "path");
        if (string.IsNullOrWhiteSpace(path))
            return NotFound();
        var diff = _state.Git?[workspaceId]?["diffs"]?[path];
        return diff is null ? NotFound() : Json(diff);
    }

    private FakeBackendResponseDto GitMutation(string workspaceId, string action)
    {
        var workspace = FindWorkspace(workspaceId);
        if (workspace is null)
            return NotFound();

        if (action is "commit")
        {
            return Json(new JsonObject
            {
                ["found"] = true,
                ["succeeded"] = true,
                ["commit"] = "f4ke0001",
                ["error"] = null
            });
        }

        if (action is "fetch" or "pull" or "push")
        {
            return Json(new JsonObject
            {
                ["found"] = true,
                ["succeeded"] = true,
                ["error"] = null,
                ["head"] = new JsonObject
                {
                    ["branch"] = workspace["branch"]?.DeepClone() ?? "main",
                    ["localBranches"] = new JsonArray("main"),
                    ["commit"] = workspace["commit"]?.DeepClone()
                }
            });
        }

        return Json(new JsonObject { ["found"] = true, ["succeeded"] = true, ["error"] = null });
    }

    private FakeBackendResponseDto AgentSession(string workspaceId)
    {
        var session = _state.Agents?[workspaceId]?["session"];
        return session is null ? NotFound() : Json(session);
    }

    private FakeBackendResponseDto SendAgentMessage(string workspaceId, string? body)
    {
        var session = _state.Agents?[workspaceId];
        if (session is null)
            return NotFound();

        var message = ReadJsonString(body, "message") ?? "";
        PublishAgent(workspaceId, "user_message", new JsonObject { ["text"] = message });
        var scripted = (session["scripts"] as JsonArray ?? [])
            .SelectMany(script => script?["events"] as JsonArray ?? [])
            .OfType<JsonObject>();
        foreach (var ev in scripted)
        {
            var type = ev["type"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(type) || ev["payload"] is not JsonNode payload)
                continue;
            PublishAgent(workspaceId, type, payload.DeepClone());
        }

        return new FakeBackendResponseDto(204, "application/json");
    }

    private FakeBackendResponseDto AgentEventStream(string workspaceId, string query)
    {
        var after = 0L;
        _ = long.TryParse(QueryValue(query, "after"), out after);
        var frames = string.Concat(AgentEventsAfter(workspaceId, after).Select(item => item.ToSseFrame()));
        return new FakeBackendResponseDto(200, "text/event-stream", frames, KeepOpen: true);
    }

    private FakeBackendResponseDto ConsoleOutput(string workspaceId, string rest)
    {
        var parts = rest.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3)
            return Json(new JsonArray());
        var key = $"{workspaceId}/{Uri.UnescapeDataString(parts[1])}";
        var lines = _state.Console?[key] as JsonArray ?? new JsonArray();
        return Json(lines);
    }

    private FakeBackendResponseDto CloneWorkspace(string? body)
    {
        var repository = ReadJsonString(body, "repository") ?? "https://git.example/demo.git";
        var name = repository.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "cloned";
        name = name.Replace(".git", "", StringComparison.OrdinalIgnoreCase);
        var workspace = new JsonObject
        {
            ["id"] = $"cloned-{Guid.NewGuid():N}"[..18],
            ["displayName"] = name,
            ["repositoryPath"] = $"/demo/{name}",
            ["worktreePath"] = $"/demo/{name}",
            ["branch"] = ReadJsonString(body, "branch") ?? "main",
            ["commit"] = "cloned01",
            ["state"] = "Stopped",
            ["applications"] = new JsonArray()
        };
        lock (_gate)
            _state.Workspaces.Add(workspace);
        PublishWorkspaces();
        return new FakeBackendResponseDto(201, "application/json", workspace.ToJsonString(WebOptions()));
    }

    private FakeBackendResponseDto IssueTicket(string? body)
    {
        var workspaceId = ReadJsonString(body, "workspaceId");
        var port = ReadJsonInt(body, "allocatedPort");
        if (workspaceId is null || port is null)
            return NotFound();
        var page = FindPageName(port.Value);
        if (page is null)
            return NotFound();
        var payload = new JsonObject
        {
            ["ticket"] = "fake-ticket",
            ["bootstrapPath"] = $"/apps/{Uri.EscapeDataString(workspaceId)}/{page}",
            ["expiresAt"] = DateTimeOffset.UtcNow.AddMinutes(30).ToString("O")
        };
        return Json(payload);
    }

    private FakeBackendResponseDto AppPage(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var page = parts.Length >= 3 ? Uri.UnescapeDataString(parts[2]) : "";
        var html = ApplicationHtml(page);
        return html is null
            ? NotFound()
            : new FakeBackendResponseDto(200, "text/html; charset=utf-8", html);
    }

    private FakeBackendResponseDto ViewerTicket(string workspaceId, string rest)
    {
        var html = ApplicationHtml("storefront") ?? "<html><body>Demo</body></html>";
        _ = workspaceId;
        _ = rest;
        var payload = new JsonObject
        {
            ["viewerUrl"] = $"{FakeServerIdentity.Url}/apps/{Uri.EscapeDataString(workspaceId)}/storefront",
            ["expiresAtUtc"] = DateTimeOffset.UtcNow.AddMinutes(30)
        };
        return Json(payload);
    }

    private JsonObject? FindWorkspace(string workspaceId)
        => _state.Workspaces
            .OfType<JsonObject>()
            .FirstOrDefault(workspace =>
                string.Equals(workspace["id"]?.GetValue<string>(), workspaceId, StringComparison.Ordinal));

    private string? FindPageName(int allocatedPort)
        => _state.Workspaces
            .SelectMany(item => item?["applications"] as JsonArray ?? [])
            .Select(application => (
                Page: application?["page"]?.GetValue<string>(),
                Ports: application?["allocatedPorts"] as JsonArray))
            .Where(item => item.Ports is not null
                && item.Ports.Any(port => port?["allocatedPort"]?.GetValue<int>() == allocatedPort))
            .Select(item => item.Page)
            .FirstOrDefault();

    private void PublishAgent(string workspaceId, string type, JsonNode payload)
    {
        FakeServerEvent added;
        Action<FakeServerEvent>[] listeners;
        lock (_gate)
        {
            _agentSequence++;
            added = new FakeServerEvent(_agentSequence, type, payload, DateTimeOffset.UtcNow);
            if (!_agentEvents.TryGetValue(workspaceId, out var events))
            {
                events = [];
                _agentEvents[workspaceId] = events;
            }

            events.Add(added);
            listeners = _agentListeners.TryGetValue(workspaceId, out var current)
                ? [.. current]
                : [];
        }

        foreach (var listener in listeners)
            listener(CloneEvent(added));
    }

    private void PublishWorkspaces()
    {
        var snapshot = WorkspaceSnapshotSse();
        Action<string>[] listeners;
        lock (_gate)
            listeners = [.. _workspaceListeners];
        foreach (var listener in listeners)
            listener(snapshot);
    }

    private string WorkspaceEventFrame(JsonNode? workspace)
    {
        if (workspace is not JsonObject obj)
            return "";
        var applications = new JsonArray(
            [.. (obj["applications"] as JsonArray ?? [])
                .OfType<JsonObject>()
                .Select(app => new JsonObject
                {
                    ["name"] = app["name"]?.DeepClone(),
                    ["state"] = app["state"]?.DeepClone(),
                    ["portHealth"] = new JsonArray(
                        [.. (app["allocatedPorts"] as JsonArray ?? [])
                            .Where(port => port?["allocatedPort"] is not null)
                            .Select(port => new JsonObject
                            {
                                ["allocatedPort"] = port!["allocatedPort"]!.DeepClone(),
                                ["healthState"] = "Healthy"
                            })])
                })]);

        var payload = new JsonObject
        {
            ["workspaceId"] = obj["id"]?.DeepClone(),
            ["state"] = obj["state"]?.DeepClone(),
            ["healthState"] = obj["healthState"]?.DeepClone() ?? "Healthy",
            ["applications"] = applications
        };
        return $"data: {payload.ToJsonString(WebOptions())}\n\n";
    }

    private static FakeServerEvent CloneEvent(FakeServerEvent item)
        => item with { Payload = item.Payload.DeepClone() };

    private static bool TryWorkspacePath(string path, out string workspaceId, out string rest)
    {
        workspaceId = "";
        rest = "";
        const string prefix = "/api/workspaces/";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
            return false;
        var remaining = path[prefix.Length..];
        if (remaining.Length == 0 || remaining == "events" || remaining.StartsWith("tutorial/", StringComparison.Ordinal))
            return false;
        var slash = remaining.IndexOf('/');
        if (slash < 0)
        {
            workspaceId = Uri.UnescapeDataString(remaining);
            return true;
        }

        workspaceId = Uri.UnescapeDataString(remaining[..slash]);
        rest = remaining[(slash + 1)..];
        return workspaceId.Length > 0;
    }

    private static string? QueryValue(string query, string name)
    {
        var trimmed = query.StartsWith('?') ? query[1..] : query;
        return trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length > 0
                && string.Equals(Uri.UnescapeDataString(parts[0]), name, StringComparison.Ordinal))
            .Select(parts => parts.Length == 2 ? Uri.UnescapeDataString(parts[1]) : "")
            .FirstOrDefault();
    }

    private static string? ReadJsonString(string? body, string name)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            return JsonNode.Parse(body)?[name]?.GetValue<string>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static int? ReadJsonInt(string? body, string name)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;
        try
        {
            return JsonNode.Parse(body)?[name]?.GetValue<int>();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private static FakeBackendResponseDto Json(JsonNode node)
        => new(200, "application/json", node.ToJsonString(WebOptions()));

    private static FakeBackendResponseDto NotFound()
        => new(404, "application/json", """{"title":"Not Found","detail":"The fake Server has no resource at that path."}""");

    private static JsonSerializerOptions WebOptions()
        => new(JsonSerializerDefaults.Web);
}

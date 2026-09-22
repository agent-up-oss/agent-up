using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.DTOs;
using AgentUp.Desktop.Features.FakeServer.Models;
using AgentUp.Desktop.Features.FakeServer.Providers;

namespace AgentUp.Desktop.Features.FakeServer.Services;

public sealed class FakeBackendService
{
    private readonly FakeServerDefinition _template;
    private readonly FakeApplicationPageProvider _pages;
    private readonly Action<int, Action> _schedule;
    private readonly Lock _gate = new();
    private FakeServerDefinition _state;
    private readonly Dictionary<string, List<FakeServerEvent>> _agentEvents = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<Action<FakeServerEvent>>> _agentListeners = new(StringComparer.Ordinal);
    private readonly List<Action<string>> _workspaceListeners = [];
    private readonly Dictionary<string, JsonArray> _appTemplates = new(StringComparer.Ordinal);
    private int _lifecycleGeneration;
    private long _agentSequence;

    public const int StartPhaseMilliseconds = 1000;

    public FakeBackendService(
        FakeServerDefinition definition,
        FakeApplicationPageProvider? pages = null,
        Action<int, Action>? schedule = null)
    {
        _template = definition;
        _state = definition.Clone();
        _pages = pages ?? new FakeApplicationPageProvider();
        _schedule = schedule ?? ((delay, work) =>
        {
            _ = Task.Delay(TimeSpan.FromMilliseconds(delay)).ContinueWith(_ => work(), TaskScheduler.Default);
        });
        CaptureTemplates();
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
            CancelLifecycle();
            CaptureTemplates();
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

    public Uri WriteApplicationPage(string workspaceId, string tabKey, string html)
        => _pages.Write(workspaceId, tabKey, html);

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
            return ListWorkspaces();
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
        if (method == "GET" && path == "/api/capabilities")
            return Json(Capabilities());
        if (method == "POST" && path == "/api/capabilities/enable")
            return EnableCapability(request.Body);
        if (method == "POST" && path.StartsWith("/api/capabilities/disable/", StringComparison.Ordinal))
            return DisableCapability(Uri.UnescapeDataString(path["/api/capabilities/disable/".Length..]));
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
            return StartWorkspace(workspaceId);
        if (method == "POST" && rest == "stop")
            return StopWorkspace(workspaceId);
        if (method == "GET" && rest == "overview")
            return Overview(workspaceId);
        if (method == "GET" && rest == "git/changes")
            return GitNode(workspaceId, "changes");
        if (method == "GET" && rest == "git/head")
            return GitHead(workspaceId);
        if (method == "GET" && rest == "git/log")
            return GitNode(workspaceId, "log");
        if (method == "GET" && rest == "commit-queue")
            return GitNode(workspaceId, "queue");
        if (method == "GET" && rest.StartsWith("git/file", StringComparison.Ordinal))
            return GitDiff(workspaceId, request.Query);
        if (method == "POST" && rest.StartsWith("git/", StringComparison.Ordinal))
            return GitMutation(workspaceId, rest["git/".Length..], request.Body);
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

    private FakeBackendResponseDto ListWorkspaces()
    {
        lock (_gate)
            return Json(new JsonArray([.. _state.Workspaces.OfType<JsonObject>().Select(PublicWorkspace)]));
    }

    private FakeBackendResponseDto WorkspaceJson(string workspaceId)
    {
        lock (_gate)
        {
            var workspace = FindWorkspace(workspaceId);
            return workspace is null ? NotFound() : Json(PublicWorkspace(workspace));
        }
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

            _appTemplates.Remove(workspaceId);
        }

        PublishWorkspaces();
        return new FakeBackendResponseDto(204, "application/json");
    }

    private FakeBackendResponseDto StartWorkspace(string workspaceId)
    {
        lock (_gate)
        {
            var workspace = FindWorkspace(workspaceId);
            if (workspace is null)
                return NotFound();
            var generation = CancelLifecycle();
            ApplyWorkspacePhase(workspace, "Starting", null, "Starting");
            _schedule(StartPhaseMilliseconds, () => ContinueStart(workspaceId, generation, checking: true));
        }

        PublishWorkspaces();
        return new FakeBackendResponseDto(204, "application/json");
    }

    private void ContinueStart(string workspaceId, int generation, bool checking)
    {
        lock (_gate)
        {
            if (generation != _lifecycleGeneration)
                return;
            var workspace = FindWorkspace(workspaceId);
            if (workspace is null)
                return;
            if (checking)
            {
                if (!string.Equals(workspace["state"]?.GetValue<string>(), "Starting", StringComparison.Ordinal))
                    return;
                ApplyWorkspacePhase(workspace, "Running", "Checking", "Checking");
                _schedule(StartPhaseMilliseconds, () => ContinueStart(workspaceId, generation, checking: false));
            }
            else
            {
                if (!string.Equals(workspace["state"]?.GetValue<string>(), "Running", StringComparison.Ordinal))
                    return;
                ApplyWorkspacePhase(workspace, "Running", "Healthy", "Running");
            }
        }

        PublishWorkspaces();
    }

    private FakeBackendResponseDto StopWorkspace(string workspaceId)
    {
        lock (_gate)
        {
            var workspace = FindWorkspace(workspaceId);
            if (workspace is null)
                return NotFound();
            CancelLifecycle();
            workspace["state"] = "Stopped";
            workspace.Remove("healthState");
            workspace["applications"] = new JsonArray();
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
            ["applicationCount"] = IsLive(workspace["state"]?.GetValue<string>()) ? applications?.Count ?? 0 : 0
        };
        return Json(payload);
    }

    private FakeBackendResponseDto GitNode(string workspaceId, string name)
    {
        lock (_gate)
        {
            var node = GitState(workspaceId)?[name];
            return node is null ? NotFound() : Json(node);
        }
    }

    private FakeBackendResponseDto GitHead(string workspaceId)
    {
        lock (_gate)
        {
            var git = GitState(workspaceId);
            return git is null ? NotFound() : Json(FakeGitProvider.Head(git));
        }
    }

    private FakeBackendResponseDto GitDiff(string workspaceId, string query)
    {
        var path = QueryValue(query, "path");
        if (string.IsNullOrWhiteSpace(path))
            return NotFound();
        lock (_gate)
        {
            var git = GitState(workspaceId);
            var diff = git is null ? null : FakeGitProvider.Diff(git, path);
            return diff is null ? NotFound() : Json(diff);
        }
    }

    private FakeBackendResponseDto GitMutation(string workspaceId, string action, string? body)
    {
        lock (_gate)
        {
            var workspace = FindWorkspace(workspaceId);
            var git = GitState(workspaceId);
            if (workspace is null || git is null)
                return NotFound();

            var result = action switch
            {
                "commit" => FakeGitProvider.Commit(git, ReadJsonStringArray(body, "files"), ReadJsonString(body, "message") ?? ""),
                "discard" => FakeGitProvider.Discard(git, ReadJsonStringArray(body, "files")),
                "fetch" => FakeGitProvider.Fetch(git),
                "pull" => FakeGitProvider.Pull(git),
                "push" => FakeGitProvider.Push(git),
                "branch" => FakeGitProvider.SwitchBranch(git, ReadJsonString(body, "name") ?? "", ReadJsonBoolean(body, "create")),
                "checkout" => FakeGitProvider.CheckoutRemote(git, ReadJsonString(body, "name") ?? ""),
                _ => new JsonObject { ["found"] = true, ["succeeded"] = true, ["error"] = null }
            };
            if (result["succeeded"]?.GetValue<bool>() == true)
            {
                workspace["commit"] = git["changes"]?["commit"]?.DeepClone();
                workspace["branch"] = git["changes"]?["branch"]?.DeepClone();
            }

            return Json(result);
        }
    }

    private FakeBackendResponseDto AgentSession(string workspaceId)
    {
        var session = _state.Agents?[workspaceId]?["session"];
        return session is null ? NotFound() : Json(session);
    }

    private FakeBackendResponseDto SendAgentMessage(string workspaceId, string? body)
    {
        string? path;
        lock (_gate)
        {
            var session = _state.Agents?[workspaceId] as JsonObject;
            var git = GitState(workspaceId);
            if (session is null)
                return NotFound();
            path = git is null ? null : FakeGitProvider.AddAgentFile(git);
        }

        var message = ReadJsonString(body, "message") ?? "";
        var snapshot = new JsonObject { ["state"] = "running" };
        PublishAgent(workspaceId, "user_message", new JsonObject { ["text"] = message });
        PublishAgent(workspaceId, "state", snapshot);
        var text = path is null
            ? "Harbor Shop is running locally."
            : $"I added `{path}` so the storefront can show the weekly harbor special. It is uncommitted in the working tree.";
        PublishAgent(workspaceId, "session_update", new JsonObject
        {
            ["sessionUpdate"] = "agent_message_chunk",
            ["content"] = new JsonObject { ["type"] = "text", ["text"] = text }
        });
        PublishAgent(workspaceId, "state", new JsonObject { ["state"] = "ready" });
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
        {
            _state.Workspaces.Add(workspace);
            _appTemplates[workspace["id"]!.GetValue<string>()] = new JsonArray();
        }
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
        var applications = IsLive(obj["state"]?.GetValue<string>())
            ? new JsonArray(
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
                                    ["healthState"] = obj["healthState"]?.DeepClone() ?? app["state"]?.DeepClone()
                                })])
                    })])
            : [];

        var payload = new JsonObject
        {
            ["workspaceId"] = obj["id"]?.DeepClone(),
            ["state"] = obj["state"]?.DeepClone(),
            ["healthState"] = obj["healthState"]?.DeepClone(),
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

    private FakeBackendResponseDto EnableCapability(string? body)
    {
        lock (_gate)
        {
            var module = FindCapability(ReadJsonString(body, "id"));
            if (module is null)
                return NotFound();
            SetCapability(module, enabled: true);
            return Json(module);
        }
    }

    private FakeBackendResponseDto DisableCapability(string id)
    {
        lock (_gate)
        {
            var module = FindCapability(id);
            if (module is null)
                return NotFound();
            SetCapability(module, enabled: false);
            return Json(module);
        }
    }

    private JsonArray Capabilities()
        => _state.Capabilities as JsonArray ?? [];

    private JsonObject? FindCapability(string? id)
        => string.IsNullOrWhiteSpace(id)
            ? null
            : Capabilities()
                .OfType<JsonObject>()
                .FirstOrDefault(module =>
                    string.Equals(module["id"]?.GetValue<string>(), id, StringComparison.Ordinal));

    private static void SetCapability(JsonObject module, bool enabled)
    {
        module["enabled"] = enabled;
        module["state"] = enabled ? "ready" : "disabled";
        module["canRun"] = enabled;
        module["messages"] = new JsonArray();
    }

    private JsonObject PublicWorkspace(JsonObject workspace)
    {
        var clone = workspace.DeepClone().AsObject();
        if (!IsLive(clone["state"]?.GetValue<string>()))
            clone["applications"] = new JsonArray();
        return clone;
    }

    private JsonObject? GitState(string workspaceId)
        => _state.Git?[workspaceId] as JsonObject;

    private void CaptureTemplates()
    {
        _appTemplates.Clear();
        foreach (var workspace in _state.Workspaces.OfType<JsonObject>())
        {
            var id = workspace["id"]?.GetValue<string>();
            if (id is null)
                continue;
            _appTemplates[id] = (workspace["applications"] as JsonArray)?.DeepClone().AsArray() ?? [];
        }
    }

    private void ApplyWorkspacePhase(JsonObject workspace, string state, string? healthState, string applicationState)
    {
        workspace["state"] = state;
        if (healthState is null)
            workspace.Remove("healthState");
        else
            workspace["healthState"] = healthState;

        var id = workspace["id"]?.GetValue<string>();
        var applications = id is not null && _appTemplates.TryGetValue(id, out var template)
            ? template.DeepClone().AsArray()
            : [];
        foreach (var app in applications.OfType<JsonObject>())
            app["state"] = applicationState;
        workspace["applications"] = applications;
    }

    private int CancelLifecycle()
        => ++_lifecycleGeneration;

    private static bool IsLive(string? state)
        => state is "Running" or "Starting";

    private static string[] ReadJsonStringArray(string? body, string name)
    {
        if (string.IsNullOrWhiteSpace(body))
            return [];
        try
        {
            return (JsonNode.Parse(body)?[name] as JsonArray ?? [])
                .Select(item => item?.GetValue<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToArray();
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return [];
        }
    }

    private static bool ReadJsonBoolean(string? body, string name)
    {
        if (string.IsNullOrWhiteSpace(body))
            return false;
        try
        {
            return JsonNode.Parse(body)?[name]?.GetValue<bool>() ?? false;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return false;
        }
    }

    private static FakeBackendResponseDto Json(JsonNode node)
        => new(200, "application/json", node.ToJsonString(WebOptions()));

    private static FakeBackendResponseDto NotFound()
        => new(404, "application/json", """{"title":"Not Found","detail":"The fake Server has no resource at that path."}""");

    private static JsonSerializerOptions WebOptions()
        => new(JsonSerializerDefaults.Web);
}

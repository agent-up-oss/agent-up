using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.Capabilities.Forgejo.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.E2E;

/// <summary>
/// A whole team on one Server, against a real Forgejo: bob writes, hector reviews the code, lucy
/// reviews the product. Nothing is faked but the agents' minds - bob and hector are the sandboxed
/// test-agent-scripted answering the Server's prompts from a playbook, lucy is the in-process
/// reviewer on a scripted chat model - so every Git and host write is the Server's own:
/// assignment, task branch, push, proposal, requested reviewers, findings at their lines, the
/// screenshot attached to the review, and the second revision going back to the same reviewers.
/// </summary>
[TestFixture]
[Category("TeamForgejoE2E")]
[NonParallelizable]
[Timeout(900_000)]
[CancelAfter(900_000)]
public sealed class TeamForgejoE2ETests
{
    private const string Scripted = "test-agent-scripted";
    private const string Finding = "Keep it short.";

    [OneTimeTearDown]
    public Task StopHost() => ForgejoLiveHost.StopSharedAsync();

    [Test]
    public async Task An_author_and_two_reviewers_take_an_issue_to_two_approvals_through_the_host()
    {
        var host = await ForgejoLiveHost.SharedAsync(CancellationToken.None);
        var repo = await host.CreateRepositoryAsync($"team-{Guid.NewGuid():N}"[..20], CancellationToken.None);
        using var clones = new TeamClones();
        await clones.SeedAsync(repo, host.Token);
        var issue = await host.CreateIssueAsync(repo.Name, "Say Ahoy", "The storefront greets visitors with Hello. Make web/greeting.txt say Ahoy.", null, CancellationToken.None);
        var members = new Dictionary<string, (string Login, string Token)>
        {
            ["bob"] = await host.CreateCollaboratorAsync(repo.Name, "bob", CancellationToken.None),
            ["hector"] = await host.CreateCollaboratorAsync(repo.Name, "hector", CancellationToken.None),
            ["lucy"] = await host.CreateCollaboratorAsync(repo.Name, "lucy", CancellationToken.None),
        };
        await using var model = ScriptedReviewModel.Start();
        await using var server = await StartServerAsync(host.Origin, model.Url);
        await Ok(server.Http.PostAsJsonAsync("api/capabilities/enable", new { id = "forgejo" }));
        var ids = new Dictionary<string, string>();
        foreach (var (name, kind, agent, rules) in Team)
        {
            var worktree = await clones.CloneAsync(repo, host.Token, name);
            ids[name] = await server.RegisterAsync(worktree, name);
            SeedCredential(server, ids[name], members[name].Token);
            await Ok(server.Http.PutAsJsonAsync($"api/workspaces/{ids[name]}/agent/member", new { kind }));
            await Ok(server.Http.PutAsJsonAsync($"api/workspaces/{ids[name]}/agent/binding", new { agent }));
            await Ok(server.Http.PutAsJsonAsync($"api/workspaces/{ids[name]}/agent/policy", new { rules }));
        }
        foreach (var id in ids.Values)
            await Ok(server.Http.PutAsJsonAsync($"api/workspaces/{id}/agent/autonomy", new { active = true }));
        var issueId = (await host.ReadAsync($"repos/{repo.Owner}/{repo.Name}/issues/{issue}", CancellationToken.None)).GetProperty("id").GetRawText();

        await Ok(server.Http.PostAsJsonAsync("api/tasks", new { workspaceId = ids["bob"], issueId }));
        var task = await WaitForTaskAsync(server, ids, task => task["stage"]?.GetValue<string>() == "approved");

        var number = task["proposal"]!["number"]!.GetValue<string>();
        var pull = await host.ReadAsync($"repos/{repo.Owner}/{repo.Name}/pulls/{number}", CancellationToken.None);
        var reviews = (await host.ReadAsync($"repos/{repo.Owner}/{repo.Name}/pulls/{number}/reviews", CancellationToken.None)).EnumerateArray().ToArray();
        var requested = reviews.First(review => Login(review) == "hector" && State(review) == "REQUEST_CHANGES");
        var findings = await host.ReadAsync($"repos/{repo.Owner}/{repo.Name}/pulls/{number}/reviews/{requested.GetProperty("id").GetRawText()}/comments", CancellationToken.None);
        var assignees = (await host.ReadAsync($"repos/{repo.Owner}/{repo.Name}/issues/{issue}", CancellationToken.None)).GetProperty("assignees").EnumerateArray().Select(user => user.GetProperty("login").GetString());
        var head = pull.GetProperty("head").GetProperty("sha").GetString();
        var lucy = reviews.Last(review => Login(review) == "lucy" && State(review) == "APPROVED");
        var lucyState = await WorkspaceStateAsync(server, ids["lucy"]);

        Assert.Multiple(() =>
        {
            Assert.That(assignees, Is.EqualTo(new[] { "bob" }), "taking the ticket assigns it to the author's account");
            Assert.That((Login(pull), pull.GetProperty("head").GetProperty("ref").GetString()), Is.EqualTo(("bob", $"agent-up/issue-{issue}")));
            Assert.That(task["proposal"]!["revision"]!.GetValue<int>(), Is.EqualTo(2), "the finding sent it back once");
            Assert.That(findings.EnumerateArray().Select(comment => (comment.GetProperty("path").GetString(), comment.GetProperty("body").GetString())),
                Does.Contain(("web/greeting.txt", Finding)));
            Assert.That(reviews.Any(review => Login(review) == "hector" && State(review) == "APPROVED" && review.GetProperty("commit_id").GetString() == head), Is.True,
                "the same dev reviewer approved the pushed fix");
            Assert.That(lucy.GetProperty("commit_id").GetString(), Is.EqualTo(head));
            Assert.That(lucy.GetProperty("body").GetString(), Does.Contain("/attachments/"), "the product review carries its screenshot");
            Assert.That((task["devReviewerWorkspaceId"]?.GetValue<string>(), task["pmReviewerWorkspaceId"]?.GetValue<string>()), Is.EqualTo((ids["hector"], ids["lucy"])));
            Assert.That(lucyState, Is.Not.EqualTo("Running"), "submitting the review stopped the reviewer's workspace");
        });
    }

    private static readonly (string Name, string Kind, string Agent, object[] Rules)[] Team =
    [
        ("bob", "Author", Scripted, [Rule("read", "WorktreeRead"), Rule("write", "WorktreeWrite")]),
        ("hector", "DevReviewer", Scripted, [Rule("read", "WorktreeRead")]),
        ("lucy", "PmReviewer", "ollama", [Rule("web", "Network")]),
    ];

    private static object Rule(string id, string kind) => new { id, kind, allow = true };

    /// <summary>
    /// What bob and hector do, by what the Server's prompt says. Bob's second prompt carries
    /// hector's finding at its line, which is the only way his fix is chosen.
    /// </summary>
    private static readonly JsonArray Playbook =
    [
        Play(["Review this change", "(revision 1,"],
            [Call("agent-up-tasks", "get_task"), Call("agent-up-orchestration", "start_workspace"),
             Call("agent-up-tasks", "submit_review", new JsonObject
             {
                 ["verdict"] = "request_changes",
                 ["summary"] = "The new greeting is too long for the header.",
                 ["findings"] = new JsonArray { new JsonObject { ["path"] = "web/greeting.txt", ["line"] = 1, ["comment"] = Finding } }
             })]),
        Play(["Review this change"],
            [Call("agent-up-tasks", "get_task"),
             Call("agent-up-tasks", "submit_review", new JsonObject { ["verdict"] = "approve", ["summary"] = "Short and correct now." })]),
        Play(["needs more work", $"web/greeting.txt:1 (hector): {Finding}"],
            [Call("agent-up-tasks", "submit_work", new JsonObject { ["summary"] = "Shortened the greeting to Ahoy." })],
            new JsonObject { ["path"] = "web/greeting.txt", ["content"] = "Ahoy\n" }),
        Play(["Issue #"],
            [Call("agent-up-tasks", "submit_work", new JsonObject { ["summary"] = "The storefront now greets visitors with Ahoy." })],
            new JsonObject { ["path"] = "web/greeting.txt", ["content"] = "Ahoy there, sailor, and welcome aboard the harbor\n" }),
    ];

    private static JsonObject Play(string[] when, JsonObject[] calls, JsonObject? write = null)
    {
        var play = new JsonObject { ["when"] = new JsonArray(when.Select(phrase => (JsonNode)phrase!).ToArray()), ["mcp"] = new JsonArray(calls) };
        if (write is not null)
            play["write"] = new JsonArray(write);
        return play;
    }

    private static JsonObject Call(string server, string tool, JsonObject? arguments = null) =>
        new() { ["server"] = server, ["tool"] = tool, ["arguments"] = arguments ?? new JsonObject() };

    private static Task<AgentUpServerProcess> StartServerAsync(string forgejo, string model)
    {
        var environment = new Dictionary<string, string>
        {
            [$"Agents__{Scripted}__Command"] = Required("AGENTUP_AGENT_E2E_TEST_AGENTS"),
            [$"Agents__{Scripted}__Arguments__0"] = "--agent",
            [$"Agents__{Scripted}__Arguments__1"] = Scripted,
            [$"Agents__{Scripted}__Arguments__2"] = "acp",
            [$"Agents__{Scripted}__Environment__AGENTUP_SCRIPTED_PLAYBOOK"] = Playbook.ToJsonString(),
            ["AGENTUP_FORGEJO_ORIGIN"] = forgejo,
            ["AGENTUP_AUTONOMY_ENABLED"] = "true",
            ["AGENTUP_OLLAMA_URL"] = model,
            ["AGENTUP_OLLAMA_MODEL"] = "scripted-reviewer",
        };
        // The sandbox clears the agent's environment, so a .NET outside the default location has
        // to be named for the published test agents to start.
        environment[$"Agents__{Scripted}__Environment__DOTNET_ROOT"] = DotnetRoot();
        if (Environment.GetEnvironmentVariable("AGENTUP_AGENT_E2E_CHROMIUM") is { Length: > 0 } chromium)
            environment["Browser__ExecutablePath"] = chromium;
        return AgentUpServerProcess.StartAsync(Path.Join(AppContext.BaseDirectory, "AgentUp.Server.dll"), environment, "Development");
    }

    /// <summary>The .NET this test runs on: three levels above its shared framework directory.</summary>
    private static string DotnetRoot() =>
        Path.GetFullPath(Path.Join(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", ".."));

    /// <summary>What a member's sign-in would have stored: its own token, never another member's.</summary>
    private static void SeedCredential(AgentUpServerProcess server, string workspaceId, string token)
    {
        var directory = Directory.CreateDirectory(Path.Join(server.DataDirectory, "forge-credentials", workspaceId)).FullName;
        File.WriteAllText(Path.Join(directory, "forgejo"), token);
    }

    /// <summary>
    /// Waits for the task to reach a state, reporting each member's loop and the queue on the way,
    /// so a run that stalls says which member stopped and why.
    /// </summary>
    private static async Task<JsonNode> WaitForTaskAsync(AgentUpServerProcess server, IReadOnlyDictionary<string, string> ids, Func<JsonNode, bool> reached)
    {
        var deadline = Stopwatch.StartNew();
        var last = "";
        while (deadline.Elapsed < TimeSpan.FromMinutes(12))
        {
            var tasks = await server.Http.GetFromJsonAsync<JsonArray>("api/tasks") ?? [];
            if (tasks.FirstOrDefault() is { } task && reached(task))
                return task;
            var loops = new List<string>();
            foreach (var (name, id) in ids)
            {
                var session = await server.Http.GetFromJsonAsync<JsonNode>($"api/workspaces/{id}/agent");
                loops.Add($"{name}: {session?["loop"]?.ToJsonString() ?? "no loop"}");
            }
            var now = $"{tasks.FirstOrDefault()?["stage"]} r{tasks.FirstOrDefault()?["proposal"]?["revision"]} | {string.Join(" | ", loops)}";
            // A member whose loop blocked does not pick the work back up on its own; waiting on would
            // only hide which member stopped and why.
            if (loops.Any(loop => loop.Contains("\"phase\":\"blocked\"", StringComparison.Ordinal)))
                Assert.Fail($"A member stopped: {now}\n{server.Output}");
            if (now != last)
                TestContext.Progress.WriteLine($"[team {deadline.Elapsed:mm\\:ss}] {now}");
            last = now;
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        Assert.Fail($"The task never reached the expected state. Last seen: {last}\n{server.Output}");
        return null!;
    }

    private static async Task<string?> WorkspaceStateAsync(AgentUpServerProcess server, string id) =>
        (await server.Http.GetFromJsonAsync<JsonNode>($"api/workspaces/{id}"))?["state"]?.GetValue<string>();

    private static async Task Ok(Task<HttpResponseMessage> call)
    {
        using var response = await call;
        if (!response.IsSuccessStatusCode)
            Assert.Fail($"{response.RequestMessage?.Method} {response.RequestMessage?.RequestUri} answered {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }

    private static string Login(JsonElement item) =>
        item.TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object && user.TryGetProperty("login", out var login)
            ? login.GetString() ?? ""
            : "";

    private static string State(JsonElement review) => review.GetProperty("state").GetString() ?? "";

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} must name the published test agents; run ./.github/scripts/run-agent-e2e.sh.");
}

/// <summary>
/// The repository the team works on, and one clone per member. Each clone's remote is the plain
/// repository URL, as a person's checkout would be: the Server sends the member's credential on
/// every push and fetch, so nothing in the clone carries one.
/// </summary>
internal sealed class TeamClones : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("agent-up-team-e2e").FullName;

    public async Task SeedAsync(ForgejoLiveRepository repo, string token)
    {
        var seed = await CloneAsync(repo, token, "seed");
        Write(seed, "agent-up.json", """
            {
              "name": "Harbor storefront",
              "applications": [
                {
                  "name": "Storefront",
                  "path": "web",
                  "command": "node server.js",
                  "ports": [ { "variable": "WEB_PORT", "defaultPort": 5810, "protocol": "http" } ]
                }
              ]
            }
            """);
        Write(seed, "web/greeting.txt", "Hello\n");
        // Node, because the Server qualifies node's script with the application's path; the page
        // reads the greeting beside it, so a change to the file is a change a visitor sees.
        Write(seed, "web/server.js", """
            const http = require('http');
            const fs = require('fs');
            const path = require('path');
            http.createServer((request, response) => {
              const greeting = fs.readFileSync(path.join(__dirname, 'greeting.txt'), 'utf8').trim();
              response.writeHead(200, { 'content-type': 'text/html' });
              response.end(`<!doctype html><title>Harbor</title><h1 id="greeting">${greeting}</h1>`);
            }).listen(Number(process.env.WEB_PORT), '127.0.0.1');
            """);
        await GitAsync(seed, "add", "--all");
        await GitAsync(seed, "commit", "-m", "Storefront");
        await GitAsync(seed, "push", "origin", "main");
    }

    public async Task<string> CloneAsync(ForgejoLiveRepository repo, string token, string name)
    {
        var path = Path.Join(_root, name);
        var authenticated = repo.CloneUrl.Replace("http://", $"http://oauth2:{token}@", StringComparison.Ordinal);
        await GitAsync(_root, "clone", "--branch", "main", authenticated, path);
        await GitAsync(path, "remote", "set-url", "origin", repo.CloneUrl);
        await GitAsync(path, "config", "user.name", name);
        await GitAsync(path, "config", "user.email", $"{name}@example.invalid");
        // The seed pushes with its token in the URL; every member clone fetches through the Server.
        if (name == "seed")
            await GitAsync(path, "remote", "set-url", "origin", authenticated);
        return path;
    }

    private static void Write(string root, string path, string content)
    {
        var target = Path.Join(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, content.ReplaceLineEndings("\n") + (content.EndsWith('\n') ? "" : "\n"));
    }

    private static async Task GitAsync(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = directory, RedirectStandardError = true, RedirectStandardOutput = true };
        foreach (var argument in arguments)
            start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error}");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TestContext.Out.WriteLine($"Could not remove {_root}: {exception.Message}");
        }
    }
}

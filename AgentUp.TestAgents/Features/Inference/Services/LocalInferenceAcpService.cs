using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using AgentUp.TestAgents.Features.Inference.Interfaces;
using AgentUp.TestAgents.Features.Inference.Models;

namespace AgentUp.TestAgents.Features.Inference.Services;

/// <summary>
/// ACP v1 over stdio for a locally-inferring agent. It sends <c>session/request_permission</c>
/// before each tool, executes on allow, and returns a typed stop reason even when the model
/// rambles, truncates, or names a tool that does not exist.
/// </summary>
public sealed class LocalInferenceAcpService(
    ITokenGenerator generator,
    IToolCallParser parser,
    IWorktreeTools tools,
    ILoopInferenceSettings settings)
{
    public const int MaxToolSteps = 8;

    public async Task RunAsync(
        TextReader input,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        var seed = settings.Seed;
        await error.WriteLineAsync($"loop-inference seed={seed}");
        await error.FlushAsync(cancellationToken);

        var pending = new ConcurrentDictionary<string, TaskCompletionSource<JsonNode>>();
        var requests = Channel.CreateUnbounded<JsonNode>();
        using var writeGate = new SemaphoreSlim(1, 1);
        using var turns = new LinkedTurnCancellation(cancellationToken);
        var worktree = ".";
        var sessionId = string.Empty;
        var history = new List<string>();
        var nextOutgoingId = 1000;

        var reader = ReadIncomingAsync(input, requests.Writer, pending, turns, cancellationToken);
        try
        {
            await foreach (var frame in requests.Reader.ReadAllAsync(cancellationToken))
            {
                await DispatchAsync(
                    frame,
                    seed,
                    history,
                    pending,
                    writeGate,
                    output,
                    turns,
                    () => ++nextOutgoingId,
                    () => sessionId,
                    id => sessionId = id,
                    () => worktree,
                    path => worktree = path,
                    cancellationToken);
            }
        }
        finally
        {
            requests.Writer.TryComplete();
            await reader;
        }
    }

    private async Task DispatchAsync(
        JsonNode frame,
        uint seed,
        List<string> history,
        ConcurrentDictionary<string, TaskCompletionSource<JsonNode>> pending,
        SemaphoreSlim writeGate,
        TextWriter output,
        LinkedTurnCancellation turns,
        Func<int> nextId,
        Func<string> sessionId,
        Action<string> setSessionId,
        Func<string> worktree,
        Action<string> setWorktree,
        CancellationToken cancellationToken)
    {
        var method = frame["method"]?.GetValue<string>();
        var id = frame["id"];
        if (method is null || method == "session/cancel")
            return;

        if (method == "session/prompt")
        {
            var response = await PromptAsync(
                id,
                sessionId(),
                worktree(),
                seed,
                history,
                frame,
                pending,
                writeGate,
                output,
                nextId,
                turns,
                cancellationToken);
            turns.BeginNext();
            await WriteAsync(output, writeGate, response, cancellationToken);
            return;
        }

        var result = method == "initialize"
            ? Initialize()
            : method == "session/new"
                ? NewSession(frame, setSessionId, setWorktree, history)
                : new JsonObject();
        await WriteAsync(output, writeGate, Result(id, result), cancellationToken);
    }

    private static async Task ReadIncomingAsync(
        TextReader input,
        ChannelWriter<JsonNode> requests,
        ConcurrentDictionary<string, TaskCompletionSource<JsonNode>> pending,
        LinkedTurnCancellation turns,
        CancellationToken cancellationToken)
    {
        while (await input.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            JsonNode? frame;
            try
            {
                frame = JsonNode.Parse(line);
            }
            catch (System.Text.Json.JsonException)
            {
                continue;
            }

            if (frame is null)
                continue;

            var method = frame["method"]?.GetValue<string>();
            if (method == "session/cancel")
            {
                turns.Cancel();
                continue;
            }

            if (method is null && frame["id"] is not null)
            {
                var responseId = frame["id"]!.ToJsonString();
                if (pending.TryRemove(responseId, out var waiter))
                    waiter.TrySetResult(frame);
                continue;
            }

            await requests.WriteAsync(frame, cancellationToken);
        }

        requests.TryComplete();
    }

    private async Task<JsonNode> PromptAsync(
        JsonNode? id,
        string sessionId,
        string worktree,
        uint seed,
        List<string> history,
        JsonNode frame,
        ConcurrentDictionary<string, TaskCompletionSource<JsonNode>> pending,
        SemaphoreSlim writeGate,
        TextWriter output,
        Func<int> nextId,
        LinkedTurnCancellation turns,
        CancellationToken cancellationToken)
    {
        var user = ReadPromptText(frame);
        history.Add("user:" + user);
        using var turn = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, turns.Token);
        for (var step = 0; step < MaxToolSteps; step++)
        {
                string completion;
                try
                {
                    completion = await generator.CompleteAsync(
                        BuildPrompt(history),
                        seed + (uint)step,
                        turn.Token);
                }
                catch (OperationCanceledException) when (turn.IsCancellationRequested)
                {
                    return Result(id, new JsonObject { ["stopReason"] = "cancelled" });
                }

                history.Add("assistant:" + completion);
                BoundHistory(history);

                if (!parser.TryParse(completion, out var call) || call is null)
                {
                    await NotifyAsync(output, writeGate, sessionId, "agent_message_chunk", completion, cancellationToken);
                    return Result(id, new JsonObject { ["stopReason"] = "end_turn" });
                }

                if (!tools.Names.Contains(call.Name))
                {
                    await NotifyAsync(output, writeGate, sessionId, "tool_call", call.Name, cancellationToken);
                    history.Add("tool:unknown tool " + call.Name);
                    return Result(id, new JsonObject { ["stopReason"] = "end_turn" });
                }

                await NotifyAsync(output, writeGate, sessionId, "tool_call", call.Name, cancellationToken);
                var allowed = await RequestPermissionAsync(
                    sessionId,
                    call.Name,
                    pending,
                    writeGate,
                    output,
                    nextId,
                    turn.Token);
                if (!allowed)
                {
                    history.Add("tool:permission denied for " + call.Name);
                    return Result(id, new JsonObject { ["stopReason"] = "end_turn" });
                }

                var executed = tools.Execute(worktree, call);
                history.Add("tool:" + executed.Output);
                await NotifyAsync(output, writeGate, sessionId, "tool_call_update", executed.Output, cancellationToken);
                if (executed.CompletesTask)
                    return Result(id, new JsonObject { ["stopReason"] = "end_turn", ["completed"] = true });
            }

        return Result(id, new JsonObject { ["stopReason"] = "max_turn_requests" });
    }

    private static async Task<bool> RequestPermissionAsync(
        string sessionId,
        string toolName,
        ConcurrentDictionary<string, TaskCompletionSource<JsonNode>> pending,
        SemaphoreSlim writeGate,
        TextWriter output,
        Func<int> nextId,
        CancellationToken cancellationToken)
    {
        var requestId = nextId();
        var waiter = new TaskCompletionSource<JsonNode>(TaskCreationOptions.RunContinuationsAsynchronously);
        pending[requestId.ToString()] = waiter;
        var request = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["id"] = requestId,
            ["method"] = "session/request_permission",
            ["params"] = new JsonObject
            {
                ["sessionId"] = sessionId,
                ["toolCall"] = new JsonObject
                {
                    ["toolCallId"] = Guid.NewGuid().ToString("N"),
                    ["title"] = toolName,
                    ["kind"] = "other",
                    ["status"] = "pending"
                },
                ["options"] = new JsonArray
                {
                    new JsonObject { ["optionId"] = "allow-once", ["name"] = "Allow once", ["kind"] = "allow_once" },
                    new JsonObject { ["optionId"] = "reject-once", ["name"] = "Reject once", ["kind"] = "reject_once" }
                }
            }
        };
        await WriteAsync(output, writeGate, request, cancellationToken);
        JsonNode response;
        try
        {
            response = await waiter.Task.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            pending.TryRemove(requestId.ToString(), out _);
            return false;
        }

        var option = response["result"]?["outcome"]?["optionId"]?.GetValue<string>();
        return string.Equals(option, "allow-once", StringComparison.Ordinal);
    }

    private static async Task NotifyAsync(
        TextWriter output,
        SemaphoreSlim writeGate,
        string sessionId,
        string kind,
        string text,
        CancellationToken cancellationToken)
    {
        var notification = new JsonObject
        {
            ["jsonrpc"] = "2.0",
            ["method"] = "session/update",
            ["params"] = new JsonObject
            {
                ["sessionId"] = sessionId,
                ["update"] = new JsonObject
                {
                    ["sessionUpdate"] = kind,
                    ["title"] = text,
                    ["content"] = new JsonObject { ["type"] = "text", ["text"] = text }
                }
            }
        };
        await WriteAsync(output, writeGate, notification, cancellationToken);
    }

    private static async Task WriteAsync(
        TextWriter output, SemaphoreSlim writeGate, JsonNode node, CancellationToken cancellationToken)
    {
        await writeGate.WaitAsync(cancellationToken);
        try
        {
            await output.WriteLineAsync(node.ToJsonString());
            await output.FlushAsync(cancellationToken);
        }
        finally
        {
            writeGate.Release();
        }
    }

    private static JsonObject Initialize() => new()
    {
        ["protocolVersion"] = 1,
        ["agentCapabilities"] = new JsonObject { ["loadSession"] = false },
        ["authMethods"] = new JsonArray()
    };

    private static JsonObject NewSession(
        JsonNode frame, Action<string> setSessionId, Action<string> setWorktree, List<string> history)
    {
        var sessionId = $"infer-session-{Guid.NewGuid():N}";
        setSessionId(sessionId);
        var cwd = frame["params"]?["cwd"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(cwd))
            setWorktree(cwd);
        history.Clear();
        return new JsonObject { ["sessionId"] = sessionId };
    }

    private static string ReadPromptText(JsonNode frame)
    {
        var prompt = frame["params"]?["prompt"];
        if (prompt is not JsonArray items)
            return string.Empty;

        return string.Join(
            '\n',
            items
                .Select(item => item?["text"]?.GetValue<string>())
                .Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    internal static string BuildPrompt(IReadOnlyList<string> history)
    {
        var body = string.Join('\n', history.TakeLast(12));
        return
            "<|im_start|>system\n" +
            "You are a tool-using agent working in a repository. Reply with exactly one tool call.\n" +
            "Tools: complete_task(summary), read_file(path), write_file(path, content), list_files(path).\n" +
            "Format:\n<tool_call>\n{\"name\": \"TOOL_NAME\", \"arguments\": {\"KEY\": \"VALUE\"}}\n</tool_call>\n" +
            "Call complete_task when the user task is done.\n" +
            "<|im_end|>\n<|im_start|>user\n" +
            body +
            "\n<|im_end|>\n<|im_start|>assistant\n";
    }

    private static void BoundHistory(List<string> history)
    {
        const int cap = 24;
        if (history.Count > cap)
            history.RemoveRange(0, history.Count - cap);
    }

    private static JsonNode Result(JsonNode? id, JsonObject result) => new JsonObject
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id?.DeepClone(),
        ["result"] = result
    };
}

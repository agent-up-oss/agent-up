using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.E2E;

/// <summary>
/// An Ollama-compatible <c>/api/chat</c> that plays a product reviewer: it reads its task, takes a
/// screenshot of the running revision, and approves with that screenshot attached. Each reply is
/// chosen by how far the conversation has come since the last prompt, so a fresh review starts
/// from the top. The screenshot's audit artifact id is read from the tool result the Server sent
/// back, exactly as a model would have to.
/// </summary>
internal sealed partial class ScriptedReviewModel : IAsyncDisposable
{
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serving;
    private int _requests;

    private ScriptedReviewModel(HttpListener listener, int port)
    {
        _listener = listener;
        Url = $"http://127.0.0.1:{port}";
        _serving = Task.Run(ServeAsync);
    }

    public string Url { get; }

    public int Requests => Volatile.Read(ref _requests);

    public static ScriptedReviewModel Start()
    {
        var (listener, port) = LoopbackListener.Start(port => $"http://127.0.0.1:{port}/");
        return new ScriptedReviewModel(listener, port);
    }

    /// <summary>The reply to one conversation: the next step after the last prompt.</summary>
    public static JsonObject Reply(JsonArray messages)
    {
        var lastPrompt = messages.Select((message, index) => (message, index))
            .LastOrDefault(entry => entry.message?["role"]?.GetValue<string>() == "user").index;
        var steps = messages.Skip(lastPrompt + 1).Count(message => message?["role"]?.GetValue<string>() == "assistant");
        var message = steps switch
        {
            0 => Call("get_task", new JsonObject()),
            1 => Call("browser_screenshot", new JsonObject()),
            2 => Call("submit_review", new JsonObject
            {
                ["verdict"] = "approve",
                ["summary"] = "I opened the storefront as a visitor. The greeting reads as the issue asks.",
                ["screenshotArtifactIds"] = new JsonArray(ArtifactIds(messages).Select(id => (JsonNode)id!).ToArray())
            }),
            _ => new JsonObject { ["role"] = "assistant", ["content"] = "Review submitted." }
        };
        return new JsonObject { ["model"] = "scripted-reviewer", ["message"] = message, ["done"] = true };
    }

    private static JsonObject Call(string tool, JsonObject arguments) => new()
    {
        ["role"] = "assistant",
        ["content"] = "",
        ["tool_calls"] = new JsonArray { new JsonObject { ["function"] = new JsonObject { ["name"] = tool, ["arguments"] = arguments } } }
    };

    /// <summary>The artifact ids screenshot results reported, newest last.</summary>
    private static IEnumerable<string> ArtifactIds(JsonArray messages) =>
        messages
            .Where(message => message?["role"]?.GetValue<string>() == "tool")
            .SelectMany(message => ArtifactId().Matches(message?["content"]?.GetValue<string>() ?? "").Select(match => match.Groups[1].Value))
            .TakeLast(1);

    [GeneratedRegex("\"ArtifactId\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex ArtifactId();

    private async Task ServeAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync().WaitAsync(_stop.Token);
            }
            catch (Exception exception) when (exception is OperationCanceledException or HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            Interlocked.Increment(ref _requests);
            using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
            var body = JsonNode.Parse(await reader.ReadToEndAsync()) as JsonObject;
            var reply = context.Request.Url?.AbsolutePath == "/api/chat" && body?["messages"] is JsonArray messages
                ? Reply(messages)
                : new JsonObject { ["error"] = "not found" };
            var bytes = Encoding.UTF8.GetBytes(reply.ToJsonString(new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            context.Response.StatusCode = reply.ContainsKey("error") ? 404 : 200;
            context.Response.ContentType = "application/json";
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        try
        {
            await _serving;
        }
        catch (OperationCanceledException)
        {
            TestContext.Out.WriteLine($"The scripted review model answered {Requests} request(s).");
        }

        _listener.Close();
        _stop.Dispose();
    }
}

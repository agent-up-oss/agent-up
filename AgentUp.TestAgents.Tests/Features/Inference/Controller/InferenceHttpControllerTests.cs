using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AgentUp.TestAgents.Tests.Support;

namespace AgentUp.TestAgents.Tests.Features.Inference.Controller;

/// <summary>The model server over real loopback HTTP, the way OpenCode reaches it.</summary>
[TestFixture]
public sealed class InferenceHttpControllerTests
{
    [Test]
    public async Task Serves_models_and_chat_completions_on_the_port_it_reports()
    {
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ready = new TaskCompletionSource<int>();
        var serving = ModelServerComposition.Controller(new ScriptedTokenGenerator("hi there"))
            .ServeAsync(0, ready.SetResult, lifetime.Token);
        var port = await ready.Task.WaitAsync(lifetime.Token);
        using var http = new HttpClient { BaseAddress = new Uri($"http://localhost:{port}/v1/") };

        var models = await http.GetFromJsonAsync<JsonNode>("models", lifetime.Token);
        using var answer = await http.PostAsJsonAsync("chat/completions", new { model = "m", messages = new[] { new { role = "user", content = "hi" } } }, lifetime.Token);
        var body = await answer.Content.ReadFromJsonAsync<JsonNode>(lifetime.Token);
        await lifetime.CancelAsync();
        await serving;

        Assert.That(models!["data"]![0]!["id"]!.GetValue<string>(), Is.EqualTo("qwen2.5-1.5b-instruct"));
        Assert.That(body!["choices"]![0]!["message"]!["content"]!.GetValue<string>(), Is.EqualTo("hi there"));
    }

    // The Server's in-process agent speaks Ollama, so the same model answers /api/chat.
    [Test]
    public async Task Answers_ollama_chat_with_the_tool_call_the_model_generated()
    {
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ready = new TaskCompletionSource<int>();
        var serving = ModelServerComposition.Controller(new ScriptedTokenGenerator("<tool_call>{\"name\": \"browser_screenshot\", \"arguments\": {}}</tool_call>"))
            .ServeAsync(0, ready.SetResult, lifetime.Token);
        var port = await ready.Task.WaitAsync(lifetime.Token);
        using var http = new HttpClient();
        var tool = new { type = "function", function = new { name = "browser_screenshot", description = "Captures the page.", parameters = new { type = "object" } } };

        using var answer = await http.PostAsJsonAsync($"http://127.0.0.1:{port}/api/chat", new { model = "m", stream = false, messages = new[] { new { role = "user", content = "look" } }, tools = new[] { tool } }, lifetime.Token);
        var body = await answer.Content.ReadFromJsonAsync<JsonNode>(lifetime.Token);
        await lifetime.CancelAsync();
        await serving;

        Assert.That(answer.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(body!["message"]!["tool_calls"]![0]!["function"]!["name"]!.GetValue<string>(), Is.EqualTo("browser_screenshot"));
    }

    [Test]
    public async Task Streams_server_sent_events_ending_in_done()
    {
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ready = new TaskCompletionSource<int>();
        var serving = ModelServerComposition.Controller(new ScriptedTokenGenerator("streamed"))
            .ServeAsync(0, ready.SetResult, lifetime.Token);
        var port = await ready.Task.WaitAsync(lifetime.Token);
        using var http = new HttpClient();

        using var answer = await http.PostAsJsonAsync($"http://127.0.0.1:{port}/v1/chat/completions", new { stream = true, messages = new[] { new { role = "user", content = "hi" } } }, lifetime.Token);
        var events = await answer.Content.ReadAsStringAsync(lifetime.Token);
        await lifetime.CancelAsync();
        await serving;

        Assert.That(answer.Content.Headers.ContentType!.MediaType, Is.EqualTo("text/event-stream"));
        Assert.That(events, Does.Contain("\"content\":\"streamed\"").And.EndWith("data: [DONE]\n\n"));
    }

    [Test]
    public async Task An_unknown_path_is_404_and_a_bad_body_is_400()
    {
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ready = new TaskCompletionSource<int>();
        var serving = ModelServerComposition.Controller(new ScriptedTokenGenerator()).ServeAsync(0, ready.SetResult, lifetime.Token);
        var port = await ready.Task.WaitAsync(lifetime.Token);
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/") };

        using var missing = await http.GetAsync("v1/embeddings", lifetime.Token);
        using var bad = await http.PostAsync("v1/chat/completions", new StringContent("not json"), lifetime.Token);
        await lifetime.CancelAsync();
        await serving;

        Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(bad.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task With_an_api_key_only_requests_bearing_it_are_answered()
    {
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ready = new TaskCompletionSource<int>();
        var serving = ModelServerComposition.Controller(new ScriptedTokenGenerator("ok"))
            .ServeAsync(0, "secret-token", ready.SetResult, lifetime.Token);
        var port = await ready.Task.WaitAsync(lifetime.Token);
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/v1/") };

        using var anonymous = await http.GetAsync("models", lifetime.Token);
        using var wrong = new HttpRequestMessage(HttpMethod.Get, "models") { Headers = { { "Authorization", "Bearer other" } } };
        using var refused = await http.SendAsync(wrong, lifetime.Token);
        using var right = new HttpRequestMessage(HttpMethod.Get, "models") { Headers = { { "Authorization", "Bearer secret-token" } } };
        using var accepted = await http.SendAsync(right, lifetime.Token);
        await lifetime.CancelAsync();
        await serving;

        Assert.That(anonymous.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(refused.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(accepted.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }
}

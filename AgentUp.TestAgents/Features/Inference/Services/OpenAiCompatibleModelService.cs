using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Inference.Interfaces;
using AgentUp.TestAgents.Features.Inference.Models;
using AgentUp.TestAgents.Features.Inference.Providers;

namespace AgentUp.TestAgents.Features.Inference.Services;

/// <summary>
/// An Ollama- and OpenAI-compatible chat-completions endpoint backed by the same pinned
/// in-process model the locally-inferring agent uses, so a real coding agent that only speaks
/// that API - OpenCode - can be driven end to end without a hosted model.
/// </summary>
public sealed class OpenAiCompatibleModelService(
    ITokenGenerator generator,
    ChatPromptProvider prompts,
    ChatCompletionReader reader,
    ChatCompletionWriter writer,
    ILoopInferenceSettings settings,
    ModelHttpListener listener)
{
    /// <summary>The model id the endpoint advertises and answers to; any requested id is accepted.</summary>
    public const string ModelId = "qwen2.5-1.5b-instruct";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private int _sequence;

    public string Models() => writer.Models(ModelId);

    /// <summary>
    /// Serves this model on loopback until cancelled, reporting the port it bound. A non-null
    /// <paramref name="apiKey"/> is required as a bearer token on every request.
    /// </summary>
    public Task ServeAsync(int port, string? apiKey, Action<int> listening, CancellationToken cancellationToken) =>
        listener.ServeAsync(port, apiKey, Models, CompleteAsync, ChatAsync, listening, cancellationToken);

    /// <summary>
    /// Answers one request. A streaming request gets its chunks through <paramref name="chunk"/>
    /// and the method returns null; otherwise the whole response is returned.
    /// </summary>
    public async Task<string?> CompleteAsync(string body, Func<string, Task> chunk, CancellationToken cancellationToken)
    {
        var request = JsonNode.Parse(body) ?? throw new JsonException("The request body is empty.");
        var model = request["model"]?.GetValue<string>() ?? ModelId;
        var completion = await GenerateAsync(request, narrowTools: true, cancellationToken);
        var id = $"chatcmpl-{Interlocked.Increment(ref _sequence)}";
        if (request["stream"]?.GetValue<bool>() != true)
            return writer.Whole(id, model, completion);

        foreach (var part in writer.Chunks(id, model, completion))
            await chunk(part);
        return null;
    }

    /// <summary>
    /// Answers Ollama's <c>/api/chat</c>, which is what the Server's in-process agent speaks.
    /// The answer is always whole: that client asks for no stream.
    /// </summary>
    public async Task<string> ChatAsync(string body, CancellationToken cancellationToken)
    {
        var request = JsonNode.Parse(body) ?? throw new JsonException("The request body is empty.");
        var model = request["model"]?.GetValue<string>() ?? ModelId;
        return writer.Ollama(model, await GenerateAsync(request, narrowTools: false, cancellationToken));
    }

    private async Task<ChatCompletion> GenerateAsync(JsonNode request, bool narrowTools, CancellationToken cancellationToken)
    {
        var offered = (request["tools"] as JsonArray ?? [])
            .Select(tool => tool?["function"]?["name"]?.GetValue<string>())
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        var prompt = prompts.Render(request, narrowTools);

        // One model, one context: requests are answered in turn.
        await _gate.WaitAsync(cancellationToken);
        string generated;
        try
        {
            generated = await generator.CompleteAsync(prompt, settings.Seed, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        return reader.Read(generated, offered, prompt.Length / 4, generated.Length / 4);
    }
}

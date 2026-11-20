using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Inference.Models;

namespace AgentUp.TestAgents.Features.Inference.Providers;

/// <summary>The OpenAI chat-completions response, whole or as server-sent-event chunks, and Ollama's chat response.</summary>
public sealed class ChatCompletionWriter
{
    public string Whole(string id, string model, ChatCompletion completion)
    {
        var message = new JsonObject { ["role"] = "assistant", ["content"] = completion.Content };
        if (completion.ToolCalls.Count > 0)
            message["tool_calls"] = ToolCalls(completion, withIndex: false);
        return Envelope(id, model, "chat.completion", new JsonObject
        {
            ["index"] = 0,
            ["message"] = message,
            ["finish_reason"] = completion.FinishReason
        }, Usage(completion)).ToJsonString();
    }

    /// <summary>The chunks in order: role and text, then the tool calls, then the finish with usage.</summary>
    public IReadOnlyList<string> Chunks(string id, string model, ChatCompletion completion)
    {
        var chunks = new List<string>
        {
            Chunk(id, model, new JsonObject { ["role"] = "assistant", ["content"] = completion.Content }, null, null)
        };
        if (completion.ToolCalls.Count > 0)
            chunks.Add(Chunk(id, model, new JsonObject { ["tool_calls"] = ToolCalls(completion, withIndex: true) }, null, null));
        chunks.Add(Chunk(id, model, new JsonObject(), completion.FinishReason, Usage(completion)));
        return chunks;
    }

    /// <summary>Ollama's <c>/api/chat</c> answer: one message, with each call's arguments as an object.</summary>
    public string Ollama(string model, ChatCompletion completion)
    {
        var message = new JsonObject { ["role"] = "assistant", ["content"] = completion.Content };
        if (completion.ToolCalls.Count > 0)
        {
            message["tool_calls"] = new JsonArray([.. completion.ToolCalls.Select(call => (JsonNode)new JsonObject
            {
                ["function"] = new JsonObject { ["name"] = call.Name, ["arguments"] = JsonNode.Parse(call.ArgumentsJson) as JsonObject ?? new JsonObject() }
            })]);
        }

        return new JsonObject
        {
            ["model"] = model,
            ["created_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["message"] = message,
            ["done"] = true,
            ["done_reason"] = "stop",
            ["prompt_eval_count"] = completion.PromptTokens,
            ["eval_count"] = completion.CompletionTokens
        }.ToJsonString();
    }

    public string Models(string model) => new JsonObject
    {
        ["object"] = "list",
        ["data"] = new JsonArray(new JsonObject { ["id"] = model, ["object"] = "model", ["owned_by"] = "agent-up" })
    }.ToJsonString();

    private static string Chunk(string id, string model, JsonObject delta, string? finish, JsonObject? usage)
        => Envelope(id, model, "chat.completion.chunk", new JsonObject
        {
            ["index"] = 0,
            ["delta"] = delta,
            ["finish_reason"] = finish
        }, usage).ToJsonString();

    private static JsonObject Envelope(string id, string model, string kind, JsonObject choice, JsonObject? usage)
    {
        var envelope = new JsonObject
        {
            ["id"] = id,
            ["object"] = kind,
            ["created"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["model"] = model,
            ["choices"] = new JsonArray(choice)
        };
        if (usage is not null)
            envelope["usage"] = usage;
        return envelope;
    }

    private static JsonArray ToolCalls(ChatCompletion completion, bool withIndex)
        => new([.. completion.ToolCalls.Select((call, index) =>
        {
            var node = new JsonObject
            {
                ["id"] = call.Id,
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = call.Name, ["arguments"] = call.ArgumentsJson }
            };
            if (withIndex)
                node["index"] = index;
            return (JsonNode)node;
        })]);

    private static JsonObject Usage(ChatCompletion completion) => new()
    {
        ["prompt_tokens"] = completion.PromptTokens,
        ["completion_tokens"] = completion.CompletionTokens,
        ["total_tokens"] = completion.PromptTokens + completion.CompletionTokens
    };
}

using System.Text.Json.Nodes;
using AgentUp.TestAgents.Features.Inference.Providers;

namespace AgentUp.TestAgents.Tests.Features.Inference.Provider;

[TestFixture]
public sealed class ChatPromptProviderTests
{
    [Test]
    public void Render_offers_tools_in_qwen_format_with_only_name_first_sentence_and_parameter_types()
    {
        var prompt = new ChatPromptProvider().Render(Request("""
            {"messages":[{"role":"user","content":"write it"}],
             "tools":[{"type":"function","function":{"name":"write","description":"Writes a file. Long detail follows.",
               "parameters":{"type":"object","properties":{"filePath":{"type":"string","description":"verbose"}},"required":["filePath"]}}}]}
            """));

        Assert.That(prompt, Does.Contain("<tools>").And.Contain("\"name\":\"write\""));
        Assert.That(prompt, Does.Contain("Writes a file.").And.Not.Contain("Long detail"));
        Assert.That(prompt, Does.Not.Contain("verbose"));
        Assert.That(prompt, Does.EndWith("<|im_start|>assistant\n"));
    }

    [Test]
    public void Render_replays_a_tool_call_and_its_result_the_way_qwen_was_trained()
    {
        var prompt = new ChatPromptProvider().Render(Request("""
            {"messages":[
              {"role":"user","content":"go"},
              {"role":"assistant","content":"","tool_calls":[{"id":"c1","type":"function","function":{"name":"read","arguments":"{\"filePath\":\"a.txt\"}"}}]},
              {"role":"tool","tool_call_id":"c1","content":"hello"}]}
            """));

        Assert.That(prompt, Does.Contain("<tool_call>\n{\"name\": \"read\", \"arguments\": {\"filePath\":\"a.txt\"}}\n</tool_call>"));
        Assert.That(prompt, Does.Contain("<tool_response>\nhello\n</tool_response>"));
    }

    [Test]
    public void Render_cuts_a_long_system_prompt_and_keeps_only_the_latest_messages()
    {
        var messages = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = new string('s', 5000) });
        for (var index = 0; index < 20; index++)
            messages.Add(new JsonObject { ["role"] = "user", ["content"] = $"message {index}" });

        var prompt = new ChatPromptProvider().Render(new JsonObject { ["messages"] = messages });

        Assert.That(prompt, Does.Not.Contain(new string('s', ChatPromptProvider.SystemPromptChars + 1)));
        Assert.That(prompt, Does.Contain("message 19").And.Not.Contain("message 11"));
    }

    [Test]
    public void Render_keeps_the_agent_tools_and_only_the_mcp_tools_the_conversation_names()
    {
        var prompt = new ChatPromptProvider().Render(Request("""
            {"messages":[{"role":"user","content":"Call the list_workspaces tool"}],
             "tools":[
               {"type":"function","function":{"name":"write","description":"Writes a file.","parameters":{"type":"object","properties":{}}}},
               {"type":"function","function":{"name":"agent-up-orchestration_list_workspaces","description":"Lists workspaces.","parameters":{"type":"object","properties":{}}}},
               {"type":"function","function":{"name":"agent-up-commits_enqueue_commit","description":"Queues a commit.","parameters":{"type":"object","properties":{}}}}]}
            """));

        Assert.That(prompt, Does.Contain("\"name\":\"write\""));
        Assert.That(prompt, Does.Contain("agent-up-orchestration_list_workspaces"));
        Assert.That(prompt, Does.Not.Contain("enqueue_commit"));
    }

    // An Ollama client sends only its own tools, so none is an MCP server's crowding the agent's.
    [Test]
    public void Render_without_narrowing_offers_every_tool_and_replays_object_arguments()
    {
        var prompt = new ChatPromptProvider().Render(Request("""
            {"messages":[{"role":"user","content":"Review it"},
               {"role":"assistant","content":"","tool_calls":[{"function":{"name":"browser_navigate","arguments":{"url":"http://app"}}}]},
               {"role":"tool","tool_name":"browser_navigate","content":"opened"}],
             "tools":[{"type":"function","function":{"name":"browser_screenshot","description":"Captures the page.","parameters":{"type":"object","properties":{}}}}]}
            """), narrowTools: false);

        Assert.That(prompt, Does.Contain("\"name\":\"browser_screenshot\""));
        Assert.That(prompt, Does.Contain("\"arguments\": {\"url\":\"http://app\"}"));
        Assert.That(prompt, Does.Contain("<tool_response>\nopened\n</tool_response>"));
    }

    [Test]
    public void Offered_keeps_an_mcp_tool_the_assistant_already_called()
    {
        var tools = JsonNode.Parse("""[{"type":"function","function":{"name":"agent-up-orchestration_get_workspace_status"}}]""")!.AsArray();
        var messages = JsonNode.Parse("""[{"role":"assistant","content":"","tool_calls":[{"function":{"name":"agent-up-orchestration_get_workspace_status","arguments":"{}"}}]}]""")!.AsArray();

        Assert.That(ChatPromptProvider.Offered(tools, messages), Has.Count.EqualTo(1));
        Assert.That(ChatPromptProvider.Offered(tools, new JsonArray()), Is.Empty);
    }

    [Test]
    public void Text_reads_only_the_text_parts_of_structured_content()
    {
        var content = JsonNode.Parse("""[{"type":"text","text":"one"},{"type":"image_url","image_url":{}},{"type":"text","text":"two"}]""");

        Assert.That(ChatPromptProvider.Text(content), Is.EqualTo("one\ntwo"));
    }

    private static JsonNode Request(string json) => JsonNode.Parse(json)!;
}

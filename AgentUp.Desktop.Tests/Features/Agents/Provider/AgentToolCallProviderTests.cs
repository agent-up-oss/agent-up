using AgentUp.Desktop.Features.Agents.Providers;

namespace AgentUp.Desktop.Tests.Features.Agents.Provider;

[TestFixture]
public sealed class AgentToolCallProviderTests
{
    [Test]
    public void An_Agent_Up_mcp_tool_reads_as_its_action_with_the_Agent_Up_source()
    {
        var view = AgentToolCallProvider.Present("agent-up-orchestration_list_workspaces", "other", null, null, "pending");

        Assert.That(view.Title, Is.EqualTo("List workspaces"));
        Assert.That(view.Source, Is.EqualTo("Agent-Up"));
        Assert.That(view.Summary, Is.EqualTo("orchestration"));
        Assert.That(view.State, Is.EqualTo("running"));
    }

    [Test]
    public void Another_mcp_server_is_named_as_the_source()
    {
        var view = AgentToolCallProvider.Present("github-api_searchIssues", "other", """{"query":"is:open label:bug"}""", null, "completed");

        Assert.That(view.Title, Is.EqualTo("Search issues"));
        Assert.That(view.Source, Is.EqualTo("Github api"));
        Assert.That(view.Summary, Is.EqualTo("is:open label:bug"));
    }

    [Test]
    public void The_agent_s_own_tool_is_named_by_its_kind_with_the_argument_that_matters()
    {
        var edit = AgentToolCallProvider.Present("/repo/src/app.ts", "edit", null, null, "completed");
        var run = AgentToolCallProvider.Present("bash", "execute", """{"command":"npm test","description":"Run the tests"}""", null, "failed");

        Assert.That(edit.Title, Is.EqualTo("Edit"));
        Assert.That(edit.Summary, Is.EqualTo("/repo/src/app.ts"));
        Assert.That(edit.Source, Is.Null);
        Assert.That(run.Title, Is.EqualTo("Run"));
        Assert.That(run.Summary, Is.EqualTo("npm test"));
        Assert.That(run.State, Is.EqualTo("failed"));
    }

    [Test]
    public void Json_output_is_indented_and_long_output_is_folded_behind_a_count()
    {
        var lines = string.Join('\n', Enumerable.Range(1, 20).Select(index => $"line {index}"));
        var json = AgentToolCallProvider.Present("x-y_z", null, """{"a":1}""", """{"state":"Running","ports":[3000]}""", "completed");
        var longText = AgentToolCallProvider.Present("x-y_z", null, null, lines, "completed");

        Assert.That(json.Output, Does.Contain("\n  \"state\": \"Running\""));
        Assert.That(json.Input, Does.Contain("\"a\": 1"));
        Assert.That(longText.HiddenLines, Is.EqualTo(20 - AgentToolCallProvider.PreviewLines));
        Assert.That(longText.Output!.Split('\n'), Has.Length.EqualTo(AgentToolCallProvider.PreviewLines));
        Assert.That(longText.FullOutput, Is.EqualTo(lines));
    }

    [Test]
    public void A_few_lines_of_overflow_are_shown_rather_than_folded()
    {
        var lines = string.Join('\n', Enumerable.Range(1, AgentToolCallProvider.PreviewLines + AgentToolCallProvider.MinimumFold - 1).Select(index => $"line {index}"));

        var view = AgentToolCallProvider.Present("x-y_z", null, null, lines, "completed");

        Assert.That(view.HiddenLines, Is.Zero);
        Assert.That(view.Output, Is.EqualTo(lines));
    }

    [Test]
    public void A_long_summary_keeps_both_ends_on_one_line()
    {
        var shortened = AgentToolCallProvider.Shorten("/home/user/projects/" + new string('a', 100) + "/file.ts");

        Assert.That(shortened, Has.Length.LessThanOrEqualTo(72));
        Assert.That(shortened, Does.StartWith("/home/user/projects/").And.EndWith("/file.ts").And.Contain("…"));
    }

    [Test]
    public void Durations_under_a_second_are_not_worth_showing()
    {
        var start = DateTimeOffset.UnixEpoch;

        Assert.That(AgentToolCallProvider.Duration(start, start.AddMilliseconds(400)), Is.Null);
        Assert.That(AgentToolCallProvider.Duration(start, start.AddSeconds(4.6)), Is.EqualTo("5s"));
        Assert.That(AgentToolCallProvider.Duration(start, start.AddSeconds(125)), Is.EqualTo("2m 5s"));
        Assert.That(AgentToolCallProvider.Duration(null, start), Is.Null);
    }

    [Test]
    public void A_write_is_named_a_write_and_shows_its_content_as_a_new_file()
    {
        var view = AgentToolCallProvider.Present(new AgentToolCallFacts("write", "notes.md", "edit", """{"filePath":"/repo/notes.md","content":"a\nb"}""", "Wrote file successfully.", "completed"));

        Assert.That(view.Title, Is.EqualTo("Write"));
        Assert.That(view.Summary, Is.EqualTo("/repo/notes.md"));
        Assert.That(view.Diff!.Added, Is.EqualTo(2));
        Assert.That(view.Diff.Removed, Is.Zero);
    }

    [Test]
    public void An_edit_prefers_the_unified_diff_over_the_replaced_snippet()
    {
        var unified = "@@ -1,1 +1,1 @@\n-listen(3000)\n+listen(3001)";
        var withUnified = AgentToolCallProvider.Present(new AgentToolCallFacts("edit", "server.ts", "edit", null, "ok", "completed", UnifiedDiff: unified, DiffOldText: "3000", DiffNewText: "3001"));
        var snippetOnly = AgentToolCallProvider.Present(new AgentToolCallFacts("edit", "server.ts", "edit", null, "ok", "completed", DiffPath: "/repo/server.ts", DiffOldText: "3000", DiffNewText: "3001"));

        Assert.That(withUnified.Diff!.Lines.Single(line => line.Kind == "added").Text, Is.EqualTo("listen(3001)"));
        Assert.That(snippetOnly.Diff!.Path, Is.EqualTo("/repo/server.ts"));
        Assert.That(snippetOnly.Diff.Lines.Select(line => line.Text), Is.EqualTo(new[] { "3000", "3001" }));
    }

    [Test]
    public void A_read_shows_the_file_text_without_OpenCode_s_tags()
    {
        var read = AgentToolCallProvider.Present(new AgentToolCallFacts("read", "a.ts", "read", null, "", "completed", RawOutput: "<path>/repo/a.ts</path>\n<type>file</type>\n<content>\nexport const port = 4000;\n</content>"));
        var search = AgentToolCallProvider.Present("glob", "search", """{"pattern":"*.md","path":"."}""", null, "completed");

        Assert.That(read.Output, Is.EqualTo("export const port = 4000;"));
        Assert.That(search.Summary, Is.EqualTo("*.md"));
    }

    [Test]
    public void Output_that_is_not_json_is_kept_as_written()
    {
        Assert.That(AgentToolCallProvider.Format("{not json"), Is.EqualTo("{not json"));
        Assert.That(AgentToolCallProvider.Format("  "), Is.Null);
        Assert.That(AgentToolCallProvider.Present(null, null, "[1]", null, null).Title, Is.EqualTo("Tool"));
    }
}

using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Providers;

namespace AgentUp.AUDebug.Tests.Features.Host.Provider;

[TestFixture]
public sealed class DebugArgParserTests
{
    private readonly DebugArgParser _parser = new();

    [Test]
    public void Parse_upDetachAndTimeout()
    {
        var (command, error) = _parser.Parse(["up", "--detach", "--timeout", "45"]);

        Assert.That(error, Is.Null);
        Assert.That(command!.Verb, Is.EqualTo("up"));
        Assert.That(command.Detach, Is.True);
        Assert.That(command.Timeout, Is.EqualTo(TimeSpan.FromSeconds(45)));
    }

    [Test]
    public void Parse_desktopStartWorkspace_joinsName()
    {
        var (command, error) = _parser.Parse(["desktop", "start-workspace", "Agent-Up"]);

        Assert.That(error, Is.Null);
        Assert.That(command!.Action, Is.EqualTo("start-workspace"));
        Assert.That(command.WorkspaceName, Is.EqualTo("Agent-Up"));
    }

    [Test]
    public void Parse_desktopOpenAgent()
    {
        var (command, error) = _parser.Parse(["desktop", "open-agent"]);

        Assert.That(error, Is.Null);
        Assert.That(command!.Action, Is.EqualTo("open-agent"));
        Assert.That(command.WorkspaceName, Is.Null);
    }

    [Test]
    public void Parse_mobileOpenAgent_joinsName()
    {
        var (command, error) = _parser.Parse(["mobile", "open-agent", "Agent-Up"]);

        Assert.That(error, Is.Null);
        Assert.That(command!.Action, Is.EqualTo("open-agent"));
        Assert.That(command.WorkspaceName, Is.EqualTo("Agent-Up"));
    }

    [Test]
    public void Parse_mobileOpenAgent_requiresName()
    {
        var (_, error) = _parser.Parse(["mobile", "open-agent"]);
        Assert.That(error, Does.Contain("requires a workspace name"));
    }

    [Test]
    public void Parse_mobileLogin_readsPassword()
    {
        var (command, error) = _parser.Parse(["mobile", "login", "--password", "test"]);

        Assert.That(error, Is.Null);
        Assert.That(command!.Password, Is.EqualTo("test"));
        Assert.That(command.Action, Is.EqualTo("login"));
    }

    [Test]
    public void Parse_docsScreenshot()
    {
        var (command, error) = _parser.Parse(["docs", "screenshot"]);

        Assert.That(error, Is.Null);
        Assert.That(command!.Surface, Is.EqualTo("docs"));
        Assert.That(command.PagePath, Is.Null);
        Assert.That(command.Heading, Is.Null);
        Assert.That(command.FullPage, Is.False);
    }

    [Test]
    public void Parse_docsScreenshot_readsPathHeadingAndFullPage()
    {
        var (command, error) = _parser.Parse([
            "docs",
            "screenshot",
            "/developer-guide/git",
            "--heading",
            "What it is",
            "--full-page"
        ]);

        Assert.That(error, Is.Null);
        Assert.That(command!.PagePath, Is.EqualTo("/developer-guide/git"));
        Assert.That(command.Heading, Is.EqualTo("What it is"));
        Assert.That(command.FullPage, Is.True);
    }

    [Test]
    public void Parse_docsScreenshot_rejectsTwoPaths()
    {
        var (command, error) = _parser.Parse(["docs", "screenshot", "/docs/workspaces", "/docs/git"]);
        Assert.That(command, Is.Null);
        Assert.That(error, Does.Contain("at most one page path"));
    }

    [Test]
    public void Parse_headingRequiresValue()
    {
        var (_, missing) = _parser.Parse(["docs", "screenshot", "--heading"]);
        var (_, flag) = _parser.Parse(["docs", "screenshot", "--heading", "--full-page"]);
        Assert.That(missing, Does.Contain("--heading requires a value"));
        Assert.That(flag, Does.Contain("--heading requires a value"));
    }

    [Test]
    public void Parse_headingOnlyForDocsScreenshot()
    {
        var (_, error) = _parser.Parse(["desktop", "screenshot", "--heading", "Git"]);
        Assert.That(error, Does.Contain("only valid for docs screenshot"));
    }

    [Test]
    public void Parse_status()
    {
        var (command, error) = _parser.Parse(["status"]);

        Assert.That(error, Is.Null);
        Assert.That(command!.Verb, Is.EqualTo("status"));
    }

    [Test]
    public void Parse_unknownAction_returnsError()
    {
        var (command, error) = _parser.Parse(["desktop", "explode"]);

        Assert.That(command, Is.Null);
        Assert.That(error, Does.Contain("unknown desktop action"));
    }

    [Test]
    public void Parse_testDefaultsToAllAndLongerTimeout()
    {
        var (command, error) = _parser.Parse(["test"]);

        Assert.That(error, Is.Null);
        Assert.That(command!.Verb, Is.EqualTo("test"));
        Assert.That(command.Suite, Is.EqualTo("all"));
        Assert.That(command.Timeout, Is.EqualTo(TimeSpan.FromSeconds(DebugLayout.TestAllTimeoutSeconds)));
    }

    [Test]
    public void Parse_testScopedUsesIterationTimeout()
    {
        var (command, error) = _parser.Parse(["test", "design-system"]);

        Assert.That(error, Is.Null);
        Assert.That(command!.Suite, Is.EqualTo("design-system"));
        Assert.That(command.Timeout, Is.EqualTo(TimeSpan.FromSeconds(DebugLayout.TestTimeoutSeconds)));
    }

    [Test]
    public void Parse_testExtraArgs_returnsError()
    {
        var (command, error) = _parser.Parse(["test", "design-system", "desktop"]);

        Assert.That(command, Is.Null);
        Assert.That(error, Does.Contain("at most one suite name"));
    }

    [Test]
    public void Parse_buildDefaultsToAllAndLongerTimeout()
    {
        var (command, error) = _parser.Parse(["build"]);

        Assert.That(error, Is.Null);
        Assert.That(command!.Verb, Is.EqualTo("build"));
        Assert.That(command.Suite, Is.EqualTo("all"));
        Assert.That(command.Timeout, Is.EqualTo(TimeSpan.FromSeconds(DebugLayout.TestAllTimeoutSeconds)));
    }

    [Test]
    public void Parse_buildScopedUsesIterationTimeout()
    {
        var (command, error) = _parser.Parse(["build", "design-system"]);

        Assert.That(error, Is.Null);
        Assert.That(command!.Verb, Is.EqualTo("build"));
        Assert.That(command.Suite, Is.EqualTo("design-system"));
        Assert.That(command.Timeout, Is.EqualTo(TimeSpan.FromSeconds(DebugLayout.TestTimeoutSeconds)));
    }

    [Test]
    public void Parse_buildExtraArgs_returnsError()
    {
        var (command, error) = _parser.Parse(["build", "design-system", "mobile"]);

        Assert.That(command, Is.Null);
        Assert.That(error, Does.Contain("at most one suite name"));
    }

    [Test]
    public void Parse_invalidTimeout_returnsError()
    {
        var (_, zero) = _parser.Parse(["up", "--timeout", "0"]);
        var (_, tooLarge) = _parser.Parse(["up", "--timeout", "601"]);
        Assert.That(zero, Does.Contain("positive integer"));
        Assert.That(tooLarge, Does.Contain("--timeout must be between"));
    }

    [Test]
    public void Parse_missingWorkspaceName_returnsError()
    {
        var (_, error) = _parser.Parse(["desktop", "start-workspace"]);
        Assert.That(error, Does.Contain("requires a workspace name"));
    }

    [Test]
    public void Parse_passwordWithoutValue_returnsError()
    {
        var (_, error) = _parser.Parse(["mobile", "login", "--password"]);
        Assert.That(error, Does.Contain("--password requires a value"));
    }

    [Test]
    public void Parse_extraHostArgs_returnsError()
    {
        var (_, error) = _parser.Parse(["up", "now"]);
        Assert.That(error, Does.Contain("does not take extra arguments"));
    }

    [Test]
    public void Parse_unknownCommand_returnsError()
    {
        var (_, error) = _parser.Parse(["explode"]);
        Assert.That(error, Does.Contain("unknown command"));
    }

    [Test]
    public void Parse_surfaceWithoutAction_returnsError()
    {
        var (_, error) = _parser.Parse(["desktop"]);
        Assert.That(error, Does.Contain("requires an action"));
    }

    [Test]
    public void Parse_screenshotExtraArgs_returnsError()
    {
        var (_, error) = _parser.Parse(["desktop", "screenshot", "extra"]);
        Assert.That(error, Does.Contain("does not take extra arguments"));
    }
}

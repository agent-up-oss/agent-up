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
    public void Parse_invalidTimeout_returnsError()
    {
        var (_, error) = _parser.Parse(["up", "--timeout", "0"]);
        Assert.That(error, Does.Contain("--timeout must be between"));
    }

    [Test]
    public void Parse_missingWorkspaceName_returnsError()
    {
        var (_, error) = _parser.Parse(["desktop", "start-workspace"]);
        Assert.That(error, Does.Contain("requires a workspace name"));
    }
}

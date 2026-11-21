using AgentUp.Server.Features.Agents.Services;

namespace AgentUp.Server.Tests.Features.Agents.Unit;

[TestFixture]
public sealed class AgentMcpSessionServiceTests
{
    [Test]
    public void Issue_returnsACredentialThatResolvesToItsWorkspace()
    {
        var sessions = new AgentMcpSessionService();

        var token = sessions.Issue("author-1");

        Assert.Multiple(() =>
        {
            Assert.That(sessions.WorkspaceFor(token), Is.EqualTo("author-1"));
            Assert.That(token, Has.Length.GreaterThanOrEqualTo(40), "256 random bits, base64url");
            Assert.That(sessions.WorkspaceFor("guess"), Is.Null);
        });
    }

    [Test]
    public void Issue_againForTheSameWorkspaceRetiresTheEarlierCredential()
    {
        var sessions = new AgentMcpSessionService();
        var first = sessions.Issue("author-1");

        var second = sessions.Issue("author-1");

        Assert.Multiple(() =>
        {
            Assert.That(sessions.WorkspaceFor(first), Is.Null);
            Assert.That(sessions.WorkspaceFor(second), Is.EqualTo("author-1"));
        });
    }

    [Test]
    public void Revoke_endsOnlyThatWorkspacesCredential()
    {
        var sessions = new AgentMcpSessionService();
        var author = sessions.Issue("author-1");
        var reviewer = sessions.Issue("reviewer-1");

        sessions.Revoke("author-1");

        Assert.Multiple(() =>
        {
            Assert.That(sessions.WorkspaceFor(author), Is.Null);
            Assert.That(sessions.WorkspaceFor(reviewer), Is.EqualTo("reviewer-1"));
        });
    }
}

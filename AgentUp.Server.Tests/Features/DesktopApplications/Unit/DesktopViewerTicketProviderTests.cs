using AgentUp.Server.Features.DesktopApplications.Providers;

namespace AgentUp.Server.Tests.Features.DesktopApplications.Unit;

[TestFixture]
public sealed class DesktopViewerTicketProviderTests
{
    [Test]
    public void Ticket_is_scoped_to_its_session()
    {
        var provider = new DesktopViewerTicketProvider();
        var issued = provider.Issue("session-one");

        Assert.That(provider.Validate("session-one", issued.Ticket), Is.True);
        Assert.That(provider.Validate("session-two", issued.Ticket), Is.False);
        Assert.That(provider.Validate("session-one", "unknown"), Is.False);
        provider.RevokeSession("session-one");
        Assert.That(provider.Validate("session-one", issued.Ticket), Is.False);
    }

    [Test]
    public void Issue_removesExpiredTickets()
    {
        var provider = new DesktopViewerTicketProvider();
        var expired = provider.Issue("session-one", DateTimeOffset.UtcNow.AddMinutes(-1));
        var current = provider.Issue("session-two");

        Assert.Multiple(() =>
        {
            Assert.That(provider.Validate("session-one", expired.Ticket), Is.False);
            Assert.That(provider.Validate("session-two", current.Ticket), Is.True);
        });
    }
}

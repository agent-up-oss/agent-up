using AgentUp.TestAgents.Features.IdentityProvider.Models;

namespace AgentUp.TestAgents.Tests.Features.IdentityProvider.Unit;

[TestFixture]
public sealed class TestIdentityRecordsTests
{
    [Test]
    public void Approving_a_device_grant_leaves_everything_else_untouched()
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(15);
        var grant = new DeviceGrant("ABCD-EFGH", "test-agent2", expires, false);

        var approved = grant.Approve();

        Assert.Multiple(() =>
        {
            Assert.That(approved.Approved, Is.True);
            Assert.That(grant.Approved, Is.False, "Approval must not mutate the grant already handed out");
            // The user code and expiry identify the grant, so an approval that changed either
            // would silently approve a different sign-in.
            Assert.That(approved.UserCode, Is.EqualTo("ABCD-EFGH"));
            Assert.That(approved.ClientId, Is.EqualTo("test-agent2"));
            Assert.That(approved.ExpiresAt, Is.EqualTo(expires));
        });
    }

    [Test]
    public void Approving_a_poll_grant_leaves_everything_else_untouched()
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(15);
        var grant = new PollGrant("test-agent4", expires, false);

        var approved = grant.Approve();

        Assert.Multiple(() =>
        {
            Assert.That(approved.Approved, Is.True);
            Assert.That(grant.Approved, Is.False);
            Assert.That(approved.ClientId, Is.EqualTo("test-agent4"));
            Assert.That(approved.ExpiresAt, Is.EqualTo(expires));
        });
    }

    [Test]
    public void A_pending_authorization_without_a_redirect_is_the_carry_the_code_by_hand_shape()
    {
        var loopback = new PendingAuthorization("test-agent1", "http://localhost:1455/auth/callback", "state", "challenge");
        var pasted = new PendingAuthorization("test-agent3", null, "state", "challenge");

        Assert.Multiple(() =>
        {
            Assert.That(loopback.RedirectUri, Is.Not.Null, "The loopback shape answers on a listener");
            Assert.That(pasted.RedirectUri, Is.Null, "The pasted shape has nowhere to redirect to");
        });
    }
}

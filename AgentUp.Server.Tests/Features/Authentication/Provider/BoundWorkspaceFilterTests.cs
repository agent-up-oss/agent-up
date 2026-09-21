using System.Security.Claims;
using AgentUp.Server.Features.Authentication.Interfaces;

namespace AgentUp.Server.Tests.Features.Authentication.Provider;

[TestFixture]
public sealed class BoundWorkspaceFilterTests
{
    [Test]
    public void Visible_ReturnsEveryItemWhenThePrincipalIsUnbound()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        var visible = BoundWorkspaceFilter.Visible(user, ["ws-a", "ws-b"], id => id);
        Assert.That(visible, Is.EqualTo(["ws-a", "ws-b"]));
    }

    [Test]
    public void Visible_ReturnsOnlyTheBoundWorkspace()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("workspace", "ws-a")]));
        var visible = BoundWorkspaceFilter.Visible(user, ["ws-a", "ws-b"], id => id);
        Assert.That(visible, Is.EqualTo(["ws-a"]));
    }
}

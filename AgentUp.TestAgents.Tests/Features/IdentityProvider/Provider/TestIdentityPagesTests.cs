using AgentUp.TestAgents.Features.IdentityProvider.Providers;

namespace AgentUp.TestAgents.Tests.Features.IdentityProvider.Provider;

// These ids are the contract a browser-driven test finds its way around by. Matching on prose
// instead would make the suite fail the moment the wording changed, so the ids are pinned here.
[TestFixture]
public sealed class TestIdentityPagesTests
{
    [Test]
    public void Consent_carriesTheFormAndTheHiddenFieldsTheApprovalNeeds()
    {
        var page = TestIdentityPages.Consent(
            "test-agent1", "/oauth/approve", "<input type=\"hidden\" name=\"state\" value=\"abc\" />");

        Assert.Multiple(() =>
        {
            Assert.That(page, Does.Contain("id=\"consent\""));
            Assert.That(page, Does.Contain("id=\"approve\""));
            Assert.That(page, Does.Contain("action=\"/oauth/approve\""));
            Assert.That(page, Does.Contain("name=\"state\""), "The hidden fields are passed straight through");
            Assert.That(page, Does.Contain("test-agent1"));
        });
    }

    [Test]
    public void DeviceEntry_offersTheFieldTheUserCodeIsTypedInto()
    {
        var page = TestIdentityPages.DeviceEntry("/device/approve");

        Assert.Multiple(() =>
        {
            Assert.That(page, Does.Contain("id=\"device\""));
            Assert.That(page, Does.Contain("id=\"user-code\""));
            Assert.That(page, Does.Contain("name=\"user_code\""));
            Assert.That(page, Does.Contain("action=\"/device/approve\""));
        });
    }

    [Test]
    public void Done_andProblem_eachCarryTheirOwnStatusElement()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TestIdentityPages.Done("You can go back to Agent-Up."), Does.Contain("id=\"status\""));
            Assert.That(TestIdentityPages.Problem("That code is not valid."), Does.Contain("id=\"error\""));
        });
    }

    [Test]
    public void CodeToCopy_showsTheCodeUnderAnIdAPasteFlowCanRead()
    {
        var page = TestIdentityPages.CodeToCopy("abc123#state");

        Assert.Multiple(() =>
        {
            Assert.That(page, Does.Contain("id=\"code\""));
            Assert.That(page, Does.Contain("abc123#state"));
        });
    }

    // Values reach these pages from the query string, so a client id carrying markup must not be
    // able to write markup into the page it is echoed on.
    [Test]
    public void Escaping_neutralisesMarkupThatArrivedInAValue()
    {
        var page = TestIdentityPages.Consent("<script>alert(1)</script>", "/oauth/approve", string.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(page, Does.Not.Contain("<script>"));
            Assert.That(page, Does.Contain("&lt;script&gt;"));
        });
    }

    [Test]
    public void Every_page_is_a_complete_document()
    {
        foreach (var page in new[]
                 {
                     TestIdentityPages.Consent("x", "/a", string.Empty),
                     TestIdentityPages.DeviceEntry("/a"),
                     TestIdentityPages.Done("done"),
                     TestIdentityPages.Problem("problem"),
                     TestIdentityPages.CodeToCopy("code")
                 })
        {
            Assert.That(page, Does.StartWith("<!DOCTYPE html>"));
            Assert.That(page, Does.Contain("<title>"));
        }
    }
}

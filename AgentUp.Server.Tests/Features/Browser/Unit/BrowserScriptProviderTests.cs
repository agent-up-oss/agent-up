using System.Text.Json;
using AgentUp.Browser.Streaming;
namespace AgentUp.Server.Tests.Features.Browser.Unit;

// These scripts are injected verbatim into a page, so the only thing standing between a selector
// and a script-injection bug is that every interpolated value goes through JSON encoding.
[TestFixture]
public sealed class BrowserScriptProviderTests
{
    private const string Hostile = "a\"]:has(x);\n//</script>";

    [Test]
    public void CheckSelector_and_CheckText_encodeTheirArgument()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BrowserScriptProvider.CheckSelector("#save"),
                Is.EqualTo($"!!document.querySelector({JsonSerializer.Serialize("#save")})"));
            Assert.That(BrowserScriptProvider.CheckText("Order placed"),
                Is.EqualTo($"(document.body.innerText||'').includes({JsonSerializer.Serialize("Order placed")})"));
        });
    }

    [TestCaseSource(nameof(SelectorScripts))]
    public void SelectorScripts_encodeAHostileSelectorRatherThanPastingIt(string script)
    {
        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain(JsonSerializer.Serialize(Hostile)));
            Assert.That(script, Does.Not.Contain("</script>"));
        });
    }

    private static IEnumerable<TestCaseData> SelectorScripts()
    {
        yield return new TestCaseData(BrowserScriptProvider.BeginMouseMove(Hostile)).SetName("BeginMouseMove");
        yield return new TestCaseData(BrowserScriptProvider.BeginAttentionPing(Hostile)).SetName("BeginAttentionPing");
        yield return new TestCaseData(BrowserScriptProvider.CompleteClick(Hostile)).SetName("CompleteClick");
        yield return new TestCaseData(BrowserScriptProvider.Click(Hostile)).SetName("Click");
        yield return new TestCaseData(BrowserScriptProvider.CheckSelector(Hostile)).SetName("CheckSelector");
    }

    [Test]
    public void Click_isCompleteClick_soTheRingIsAlwaysCleanedUp()
    {
        Assert.That(BrowserScriptProvider.Click("#save"), Is.EqualTo(BrowserScriptProvider.CompleteClick("#save")));
    }

    [Test]
    public void BeginMouseMove_animatesThePointerToTheElementCentre()
    {
        var script = BrowserScriptProvider.BeginMouseMove("#save");

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("__agentUpMouse"));
            Assert.That(script, Does.Contain("scrollIntoView"));
            Assert.That(script, Does.Contain($"transition:left {BrowserScriptProvider.AnimationMs}ms linear"));
            Assert.That(script, Does.Contain("Element not found: "));
        });
    }

    [Test]
    public void BeginAttentionPing_expandsAndFadesARingAtTheElement()
    {
        var script = BrowserScriptProvider.BeginAttentionPing("#save");

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("__agentUpClickRing"));
            Assert.That(script, Does.Contain($"transition:transform {BrowserScriptProvider.AnimationMs}ms ease-out"));
            Assert.That(script, Does.Contain("scale(1.8)"));
        });
    }

    [Test]
    public void CompleteClick_refusesADisabledElementAndAlwaysRemovesTheRing()
    {
        var script = BrowserScriptProvider.CompleteClick("#save");

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("matches(':disabled')"));
            Assert.That(script, Does.Contain("Element is disabled: "));
            Assert.That(script, Does.Contain("finally{if(c)c.remove();}"));
            Assert.That(script, Does.Contain("Click failed: "));
        });
    }

    [Test]
    public void Fill_setsTheValueThroughThePrototypeSetterAndNotifiesTheFramework()
    {
        var script = BrowserScriptProvider.Fill("#email", "a@b.test");

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain(JsonSerializer.Serialize("a@b.test")));
            Assert.That(script, Does.Contain("HTMLTextAreaElement.prototype"));
            Assert.That(script, Does.Contain("getOwnPropertyDescriptor"));
            Assert.That(script, Does.Contain("new Event('input',{bubbles:true})"));
            Assert.That(script, Does.Contain("new Event('change',{bubbles:true})"));
        });
    }

    [Test]
    public void Fill_encodesAHostileValue()
    {
        Assert.That(BrowserScriptProvider.Fill("#email", Hostile), Does.Contain(JsonSerializer.Serialize(Hostile)));
    }

    [Test]
    public void Press_dispatchesTheFullKeySequenceToTheActiveElement()
    {
        var script = BrowserScriptProvider.Press("Enter");

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("document.activeElement||document.body"));
            Assert.That(script, Does.Contain("['keydown','keypress','keyup']"));
            Assert.That(script, Does.Contain($"key:{JsonSerializer.Serialize("Enter")}"));
        });
    }

    [Test]
    public void InspectPage_reportsThePageAndRedactsSensitiveValues()
    {
        Assert.Multiple(() =>
        {
            Assert.That(BrowserScriptProvider.InspectPage, Does.Contain("password:1,hidden:1"));
            Assert.That(BrowserScriptProvider.InspectPage, Does.Contain("token|secret|key|auth|credential|passwd|cvv|ssn"));
            Assert.That(BrowserScriptProvider.InspectPage, Does.Contain("title:document.title"));
            Assert.That(BrowserScriptProvider.GetUrl, Is.EqualTo("window.location.href"));
            Assert.That(BrowserScriptProvider.CheckNavigation, Is.EqualTo("document.readyState"));
        });
    }
}

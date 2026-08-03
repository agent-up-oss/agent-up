using AgentUp.Desktop.Features.Browser.Resources;

namespace AgentUp.Desktop.Tests.Features.Browser.Unit;

[TestFixture]
public class BrowserScriptsTests
{
    [Test]
    public void CompleteClick_cleansUpAndReportsMissingOrDisabledTargets()
    {
        var script = BrowserScripts.CompleteClick("button.save");

        Assert.Multiple(() =>
        {
            Assert.That(script, Does.Contain("if(!e){if(c)c.remove();return JSON.stringify({error:'Element not found: '"));
            Assert.That(script, Does.Contain("if(e.matches(':disabled')){if(c)c.remove();return JSON.stringify({error:'Element is disabled: '"));
            Assert.That(script, Does.Contain("catch(ex){return JSON.stringify({error:'Click failed: '"));
            Assert.That(script, Does.Contain("finally{if(c)c.remove();}"));
        });
    }
}

using AgentUp.Server.Features.Validation.Controllers;
namespace AgentUp.Server.Tests.Features.Validation.Controller;
public sealed class ValidationMcpToolsTests
{
    [Test]
    public void Save_guidance_requires_behavioral_gui_outcomes_and_rerecording()
    {
        var description = typeof(ValidationMcpTools).GetMethod(nameof(ValidationMcpTools.Save))!.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false).Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        Assert.Multiple(() => { Assert.That(description, Does.Contain("user intent")); Assert.That(description, Does.Contain("visible GUI outcomes")); Assert.That(description, Does.Contain("not implementation details")); Assert.That(description, Does.Contain("re-record")); });
    }
}

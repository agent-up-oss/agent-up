using AgentUp.Server.Features.Validation.Controllers;
namespace AgentUp.Server.Tests.Features.Validation.Controller;
public sealed class ValidationMcpToolsTests
{
    [Test]
    public void Save_guidance_requires_goal_oriented_recording()
    {
        var description = typeof(ValidationMcpTools).GetMethod(nameof(ValidationMcpTools.Save))!.GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false).Cast<System.ComponentModel.DescriptionAttribute>().Single().Description;
        Assert.Multiple(() =>
        {
            Assert.That(description, Does.Contain("user's goal"));
            Assert.That(description, Does.Contain("source"));
            Assert.That(description, Does.Contain("Do not inspect every route"));
            Assert.That(description, Does.Contain("re-record"));
        });
    }
}

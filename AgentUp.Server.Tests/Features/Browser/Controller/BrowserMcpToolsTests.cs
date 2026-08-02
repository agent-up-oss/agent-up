using AgentUp.Server.Features.Browser.Controllers;

namespace AgentUp.Server.Tests.Features.Browser.Controller;

[TestFixture]
public sealed class BrowserMcpToolsTests
{
    [Test]
    public void BrowserToolDescriptions_TellAgentsToInspectConsoleAfterFailure()
    {
        var methodNames = new[]
        {
            nameof(BrowserMcpTools.Navigate),
            nameof(BrowserMcpTools.InspectPage),
            nameof(BrowserMcpTools.Click),
            nameof(BrowserMcpTools.Fill),
            nameof(BrowserMcpTools.Press),
            nameof(BrowserMcpTools.WaitForSelector),
            nameof(BrowserMcpTools.WaitForText),
            nameof(BrowserMcpTools.WaitForNavigation),
            nameof(BrowserMcpTools.Screenshot)
        };

        foreach (var methodName in methodNames)
        {
            var description = typeof(BrowserMcpTools)
                .GetMethod(methodName)!
                .GetCustomAttributes(typeof(System.ComponentModel.DescriptionAttribute), false)
                .Cast<System.ComponentModel.DescriptionAttribute>()
                .Single()
                .Description;

            Assert.That(description, Does.Contain("console immediately"), methodName);
            Assert.That(description, Does.Contain("Orchestration MCP"), methodName);
        }
    }
}

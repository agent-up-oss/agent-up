using System.ComponentModel;
using AgentUp.Server.Features.Diagnostics.Controllers;

namespace AgentUp.Server.Tests.Features.Diagnostics.Controller;

[TestFixture]
public sealed class DiagnosticsMcpToolsTests
{
    [Test]
    public void ToolContract_DescribesCompleteScopedDiagnostics()
    {
        var description = typeof(DiagnosticsMcpTools)
            .GetMethod(nameof(DiagnosticsMcpTools.GetWorkspaceDiagnostics))!
            .GetCustomAttributes(typeof(DescriptionAttribute), false)
            .Cast<DescriptionAttribute>()
            .Single().Description;

        Assert.Multiple(() =>
        {
            Assert.That(description, Does.Contain("process state"));
            Assert.That(description, Does.Contain("JavaScript exceptions"));
            Assert.That(description, Does.Contain("failed network requests"));
            Assert.That(description, Does.Contain("browser session"));
            Assert.That(description, Does.Contain("active from resolved"));
        });
    }
}

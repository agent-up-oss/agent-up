using AgentUp.AUDebug.Features.Host.DTOs;
using AgentUp.AUDebug.Features.Host.Services;

namespace AgentUp.AUDebug.Tests.Features.Host.Unit;

[TestFixture]
public sealed class DebugOutputServiceTests
{
    [Test]
    public void Write_printsArtifactPath()
    {
        using var output = new StringWriter();
        var exit = new DebugOutputService(output).Write(CommandResultDto.Ok("captured", "/tmp/desktop.png"));

        Assert.Multiple(() =>
        {
            Assert.That(exit, Is.EqualTo(0));
            Assert.That(output.ToString(), Does.Contain("captured"));
            Assert.That(output.ToString(), Does.Contain("screenshot: /tmp/desktop.png"));
        });
    }

    [Test]
    public void WriteHelp_listsSurfaceCommands()
    {
        using var output = new StringWriter();
        var exit = new DebugOutputService(output).WriteHelp();

        Assert.That(exit, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("mobile login"));
        Assert.That(output.ToString(), Does.Contain("desktop open-agent"));
        Assert.That(output.ToString(), Does.Contain("mobile open-agent"));
        Assert.That(output.ToString(), Does.Contain("status"));
    }

    [Test]
    public void WriteHelp_listsTestAndBuildCommands()
    {
        using var output = new StringWriter();
        new DebugOutputService(output).WriteHelp();

        Assert.That(output.ToString(), Does.Contain("test <suite>"));
        Assert.That(output.ToString(), Does.Contain("test all"));
        Assert.That(output.ToString(), Does.Contain("build design-system"));
        Assert.That(output.ToString(), Does.Contain("build mobile"));
    }

    [Test]
    public void WriteHelp_listsDocsScreenshotFlags()
    {
        using var output = new StringWriter();
        new DebugOutputService(output).WriteHelp();

        Assert.That(output.ToString(), Does.Contain("docs screenshot [path]"));
        Assert.That(output.ToString(), Does.Contain("--full-page"));
        Assert.That(output.ToString(), Does.Contain("--heading <text>"));
    }
}

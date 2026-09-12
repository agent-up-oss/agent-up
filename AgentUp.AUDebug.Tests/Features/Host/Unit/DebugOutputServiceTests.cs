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
        Assert.That(output.ToString(), Does.Contain("status"));
    }
}

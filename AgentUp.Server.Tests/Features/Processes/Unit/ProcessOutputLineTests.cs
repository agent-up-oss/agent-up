using AgentUp.Server.Features.Processes.Models;

namespace AgentUp.Server.Tests.Features.Processes.Unit;

[TestFixture]
public sealed class ProcessOutputLineTests
{
    [Test]
    public void StreamName_isStdoutUnlessTheLineIsStderr()
    {
        Assert.That(new ProcessOutputLine("out", ProcessOutputStream.Stdout).StreamName, Is.EqualTo("stdout"));
        Assert.That(new ProcessOutputLine("err", ProcessOutputStream.Stderr).StreamName, Is.EqualTo("stderr"));
    }
}

using AgentUp.InstallerApp.Features.Installation.Services;

namespace AgentUp.InstallerApp.Tests.Features.Installation.Unit;

[TestFixture]
public sealed class InstallerCommandLineServiceTests
{
    [Test]
    public void ShouldRunCommandLine_detectsInstallerCommandArguments()
    {
        var service = new InstallerCommandLineService();

        Assert.That(service.ShouldRunCommandLine(["--payload-root", "/payload", "--install-core"]), Is.True);
        Assert.That(service.ShouldRunCommandLine(["--smoke-installer-operations"]), Is.True);
        Assert.That(service.ShouldRunCommandLine(["--install-component", "cli"]), Is.True);
        Assert.That(service.ShouldRunCommandLine(["--payload-root", "/payload"]), Is.False);
    }
}

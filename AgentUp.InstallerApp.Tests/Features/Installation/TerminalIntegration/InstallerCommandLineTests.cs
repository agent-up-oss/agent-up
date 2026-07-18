using AgentUp.InstallerApp.Features.Installation.Services;
using AgentUp.Installers.Features.Installation.Providers;

namespace AgentUp.InstallerApp.Tests.Features.Installation.TerminalIntegration;

[TestFixture]
public class InstallerCommandLineTests
{
    [Test]
    public void ShouldRunInstallCore_detectsInstallCoreArgument()
    {
        Assert.That(InstallerCommandLine.ShouldRunInstallCore(["--payload-root", "/payload", "--install-core"]), Is.True);
        Assert.That(InstallerCommandLine.ShouldRunInstallCore(["--payload-root", "/payload"]), Is.False);
    }

    [Test]
    public async Task RunInstallCoreAsync_executesAdapterAndReportsSuccess()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await InstallerCommandLine.RunInstallCoreAsync(
            new FakeInstallerPlatformAdapter("Test"),
            output,
            error);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output.ToString(), Does.Contain("Core app installation succeeded."));
        Assert.That(error.ToString(), Is.Empty);
    }
}

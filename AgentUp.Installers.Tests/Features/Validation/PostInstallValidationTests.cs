using AgentUp.Installers.Features.Validation;

namespace AgentUp.Installers.Tests.Features.Validation;

[TestFixture]
public class PostInstallValidationTests
{
    [Test]
    public void Validate_succeeds_whenInstalledStateMatchesContract()
    {
        var version = new Version(1, 2, 3);
        var report = PostInstallValidation.Validate(new InstalledState(
            ServiceRegistered: true,
            ServiceRunning: true,
            CliAvailableFromFreshShell: true,
            DesktopInstalled: true,
            InstallerVersion: version,
            CliVersion: version,
            ServerVersion: version,
            DesktopVersion: version), version);

        Assert.That(report.Succeeded, Is.True);
        Assert.That(report.Findings, Is.Empty);
    }

    [Test]
    public void Validate_reportsEveryRequiredFailure()
    {
        var report = PostInstallValidation.Validate(new InstalledState(
            ServiceRegistered: false,
            ServiceRunning: false,
            CliAvailableFromFreshShell: false,
            DesktopInstalled: false,
            InstallerVersion: new Version(1, 0, 0),
            CliVersion: null,
            ServerVersion: new Version(1, 2, 3),
            DesktopVersion: new Version(1, 2, 4)), new Version(1, 2, 3));

        Assert.That(report.Succeeded, Is.False);
        Assert.That(report.Findings.Select(f => f.Code), Does.Contain("service.missing"));
        Assert.That(report.Findings.Select(f => f.Code), Does.Contain("service.stopped"));
        Assert.That(report.Findings.Select(f => f.Code), Does.Contain("cli.path"));
        Assert.That(report.Findings.Select(f => f.Code), Does.Contain("desktop.missing"));
        Assert.That(report.Findings.Select(f => f.Code), Does.Contain("installer.version"));
        Assert.That(report.Findings.Select(f => f.Code), Does.Contain("cli.version"));
        Assert.That(report.Findings.Select(f => f.Code), Does.Contain("desktop.version"));
    }
}

using LocalInstaller.Smoke.Features.InstalledServiceValidation.Factories;
using LocalInstaller.Smoke.Features.InstalledServiceValidation.Services;
using LocalInstaller.Smoke.Features.PackageValidation.Interfaces;
using LocalInstaller.Smoke.Tests.Features.InstalledServiceValidation.Fake;
using LocalInstaller.Smoke.Tests.Features.PackageValidation.Fake;
using LocalInstaller.Smoke.Tests.Features.RuntimeSecurity.Fake;

namespace LocalInstaller.Smoke.Tests.Features.InstalledServiceValidation.Provider;

[TestFixture]
public sealed class InstalledServiceSmokeValidatorFactoryTests
{
    [Test]
    public void Create_returnsSkippedValidatorForMacOsInstallerAppOnlyPackage()
    {
        var validator = InstalledServiceSmokeValidatorFactory.Create(
            "macos",
            new RecordingCommandRunner((_, _) => new CommandResult(0, "", "")),
            new FakeServerProbe("http://127.0.0.1:5000"),
            new NullRuntimeSecurityChecks());

        Assert.That(validator, Is.TypeOf<SkippedInstalledServiceSmokeValidator>());
    }

    [Test]
    public void Create_returnsWindowsValidatorForMsiSidecarInstalledServiceSmoke()
    {
        var validator = InstalledServiceSmokeValidatorFactory.Create(
            "windows",
            new RecordingCommandRunner((_, _) => new CommandResult(0, "", "")),
            new FakeServerProbe("http://127.0.0.1:5000"),
            new NullRuntimeSecurityChecks());

        Assert.That(validator, Is.TypeOf<WindowsInstalledServiceSmokeValidator>());
    }
}

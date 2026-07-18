using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Factories;
using AgentUp.PackageSmoke.Features.InstalledServiceValidation.Services;
using AgentUp.PackageSmoke.Features.PackageValidation.Interfaces;
using AgentUp.PackageSmoke.Tests.Features.PackageValidation;

namespace AgentUp.PackageSmoke.Tests.Features.InstalledServiceValidation;

[TestFixture]
public sealed class InstalledServiceSmokeValidatorFactoryTests
{
    [Test]
    public void Create_returnsSkippedValidatorForMacOsInstallerAppOnlyPackage()
    {
        var validator = InstalledServiceSmokeValidatorFactory.Create(
            "macos",
            new RecordingCommandRunner((_, _) => new CommandResult(0, "", "")),
            new FakeServerProbe("http://127.0.0.1:5000"));

        Assert.That(validator, Is.TypeOf<SkippedInstalledServiceSmokeValidator>());
    }
}

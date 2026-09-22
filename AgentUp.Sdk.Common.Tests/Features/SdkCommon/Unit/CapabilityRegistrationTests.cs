using AgentUp.Sdk.Common;

namespace AgentUp.Sdk.Common.Tests.Features.SdkCommon.Unit;

[TestFixture]
public sealed class CapabilityRegistrationTests
{
    [Test]
    public void Registration_records_the_implementation_type()
    {
        var registration = new CapabilityRegistration<SampleCapability>();

        Assert.That(registration.ImplementationType, Is.EqualTo(typeof(SampleCapability)));
    }

    [Test]
    public void Project_exposes_registered_implementations()
    {
        ICapabilityRegistration registration = new CapabilityRegistration<SampleCapability>();
        var project = new CapabilityProject([registration]);

        Assert.That(project.Registrations.Single().ImplementationType, Is.EqualTo(typeof(SampleCapability)));
    }

    private sealed class SampleCapability;
}

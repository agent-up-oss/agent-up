using AgentUp.Desktop.Features.Authentication.DTOs;

namespace AgentUp.Desktop.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class ClientSurfaceAvailabilityTests
{
    [Test]
    public void Demo_exposesValidationSidebarAndHidesTabOnlySurfaces()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ClientSurfaceAvailability.Demo.Validation, Is.True);
            Assert.That(ClientSurfaceAvailability.Demo.Database, Is.False);
            Assert.That(ClientSurfaceAvailability.Demo.Diagnostics, Is.False);
            Assert.That(ClientSurfaceAvailability.Demo.Metrics, Is.False);
        });
    }

    [Test]
    public void ForActiveServer_hidesDesktopOnlyChromeOnDemo()
        => Assert.That(ClientSurfaceAvailability.ForActiveServer(true), Is.EqualTo(ClientSurfaceAvailability.Demo));

    [Test]
    public void ForActiveServer_exposesDesktopOnlyChromeOnARealServer()
        => Assert.That(ClientSurfaceAvailability.ForActiveServer(false), Is.EqualTo(ClientSurfaceAvailability.Real));
}

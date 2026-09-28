using AgentUp.Desktop.Features.Authentication.DTOs;

namespace AgentUp.Desktop.Tests.Features.Authentication.Unit;

[TestFixture]
public sealed class ClientSurfaceAvailabilityTests
{
    [Test]
    public void ForActiveServer_hidesDesktopOnlyChromeOnDemo()
        => Assert.That(ClientSurfaceAvailability.ForActiveServer(true), Is.EqualTo(ClientSurfaceAvailability.Demo));

    [Test]
    public void ForActiveServer_exposesDesktopOnlyChromeOnARealServer()
        => Assert.That(ClientSurfaceAvailability.ForActiveServer(false), Is.EqualTo(ClientSurfaceAvailability.Real));
}

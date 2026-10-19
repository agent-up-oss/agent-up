using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;
using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class PlatformCapabilityCacheTests
{
    [Test]
    public void Toolchain_probesOncePerIdForTheRun()
    {
        var probe = new CountingProbe();
        var platform = new PlatformCapabilityProvider(probe);

        platform.Toolchain("dotnet");
        platform.Toolchain("dotnet");
        platform.Toolchain("git");

        Assert.Multiple(() =>
        {
            Assert.That(probe.Calls, Is.EqualTo(2));
            Assert.That(platform.Toolchain("dotnet").Version, Is.EqualTo("9.0.0"));
        });
    }

    private sealed class CountingProbe : IToolchainProbe
    {
        public int Calls { get; private set; }

        public ToolchainObservation Probe(string toolchainId)
        {
            Calls++;
            return new ToolchainObservation(true, "9.0.0");
        }

        public ToolchainObservation Probe(string toolchainId, string? path)
            => Probe(toolchainId);
    }
}

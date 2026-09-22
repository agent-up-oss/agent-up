using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Server.Features.Capabilities.Providers;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Capabilities.Unit;

[TestFixture]
public sealed class CapabilityModuleLoadProviderTests
{
    [Test]
    public void Load_returns_null_when_the_manifest_has_no_module()
    {
        var loaded = new CapabilityModuleLoadProvider().Load("/packages/dotnet/1.0.0", new CapabilityPackageManifest
        {
            Id = "dotnet",
            Version = "1.0.0",
            Kind = "runtime"
        });

        Assert.That(loaded, Is.Null);
    }
}

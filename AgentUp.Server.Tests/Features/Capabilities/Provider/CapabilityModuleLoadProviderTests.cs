using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Server.Features.Capabilities.Providers;

namespace AgentUp.Server.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilityModuleLoadProviderTests
{
    [Test]
    public void Load_returns_null_when_the_module_file_is_missing()
    {
        var directory = Path.Join(Path.GetTempPath(), "agent-up-cap-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var loaded = new CapabilityModuleLoadProvider().Load(directory, new CapabilityPackageManifest
            {
                Id = "dotnet",
                Version = "1.0.0",
                Kind = "runtime",
                Module = "module.dll"
            });

            Assert.That(loaded, Is.Null);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

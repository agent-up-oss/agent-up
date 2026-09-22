using AgentUp.Server.Features.Capabilities.DTOs;
using AgentUp.Server.Features.Capabilities.Providers;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;

namespace AgentUp.Server.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilityEnabledSetStoreTests
{
    [Test]
    public void Write_then_read_round_trips_enabled_modules()
    {
        var root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-enabled-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var store = new CapabilityEnabledSetStore(root);
            store.Write(new EnabledCapabilitySetDto
            {
                Modules = [new CapabilityPackageRef("dotnet", "1.0.0")]
            });

            Assert.That(store.Read().Modules.Single().Id, Is.EqualTo("dotnet"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void Missing_file_returns_an_empty_set()
    {
        var root = Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-enabled-" + Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var store = new CapabilityEnabledSetStore(root);

            Assert.That(store.Read().Modules, Is.Empty);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}

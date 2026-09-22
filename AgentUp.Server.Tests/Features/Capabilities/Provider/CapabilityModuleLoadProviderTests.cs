using System.Reflection;
using AgentUp.Capabilities.Abstractions.Features.Capabilities.Models;
using AgentUp.Server.Features.Capabilities.Providers;

namespace AgentUp.Server.Tests.Features.Capabilities.Provider;

[TestFixture]
public sealed class CapabilityModuleLoadProviderTests
{
    /// <summary>
    /// The packed DLL is the launch contract, and resolving a capability out of it is the whole
    /// dynamic capability model. A packer that writes no module name, or an assembly carrying no
    /// project definition, leaves the Server with no runtimes at all - which reads downstream as
    /// an `agent-up.json` whose runtime sections simply do not exist.
    /// </summary>
    /// <remarks>
    /// This assembly is the module: it already implements both SDK contracts, so the real
    /// reflection path runs without a project reference to a first-party capability, which the
    /// Server is not allowed to take.
    /// </remarks>
    [Test]
    public void Load_resolves_both_capabilities_from_a_packed_module()
    {
        var module = Assembly.GetExecutingAssembly().Location;

        var loaded = new CapabilityModuleLoadProvider().Load(
            Path.GetDirectoryName(module)!,
            new CapabilityPackageManifest
            {
                Id = "dotnet",
                Version = "1.0.0",
                Kind = "runtime",
                Module = Path.GetFileName(module)
            });

        Assert.That(loaded, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(loaded!.Runtime?.Identity.Id, Is.EqualTo("dotnet"));
            Assert.That(loaded.Runtime?.SectionName, Is.EqualTo("dotnet"));
            Assert.That(loaded.Agent?.Identity.Id, Is.EqualTo("codex"));
        });
    }

    [Test]
    public void Load_returns_null_for_a_module_outside_the_package_directory()
    {
        var module = Assembly.GetExecutingAssembly().Location;
        var directory = Path.Join(Path.GetDirectoryName(module)!, "packages", "dotnet");
        Directory.CreateDirectory(directory);

        var loaded = new CapabilityModuleLoadProvider().Load(directory, new CapabilityPackageManifest
        {
            Id = "dotnet",
            Version = "1.0.0",
            Kind = "runtime",
            Module = Path.Join("..", "..", Path.GetFileName(module))
        });

        Assert.That(loaded, Is.Null, "a manifest must not reach outside its own package directory");
    }

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

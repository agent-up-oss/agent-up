using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Providers;
using AgentUp.Capabilities.Dotnet.Features.DotnetCapability.Services;

namespace AgentUp.Capabilities.Dotnet.Tests.Features.DotnetCapability.Provider;

[TestFixture]
public sealed class DotnetCapabilityPackageWriterTests
{
    [Test]
    public void Write_creates_capability_json()
    {
        var directory = CreateDirectory();
        try
        {
            var packer = new DotnetCapabilityPacker();
            new DotnetCapabilityPackageWriter().Write(directory, packer.Manifest(), packer.DefaultNix());

            Assert.That(File.Exists(Path.Join(directory, "capability.json")), Is.True);
            Assert.That(File.Exists(Path.Join(directory, "AgentUp.Capabilities.Dotnet.dll"))
                        || Directory.EnumerateFiles(directory, "*.dll").Any(), Is.True);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Write_creates_default_nix()
    {
        var directory = CreateDirectory();
        try
        {
            var packer = new DotnetCapabilityPacker();
            new DotnetCapabilityPackageWriter().Write(directory, packer.Manifest(), packer.DefaultNix());

            Assert.That(File.ReadAllText(Path.Join(directory, "default.nix")), Does.Contain("mkShell"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    // The module file is what makes the package dynamic: the Server loads it to get
    // IRuntimeCapability. A manifest that names none is a package with nothing to copy, and a
    // module directory that does not hold the SDK assemblies still produces a package rather
    // than throwing halfway through writing one.
    [Test]
    public void Write_copies_no_module_when_the_manifest_names_none()
    {
        var directory = CreateDirectory();
        try
        {
            var packer = new DotnetCapabilityPacker();
            var manifest = packer.Manifest() with { Module = "" };

            new DotnetCapabilityPackageWriter().Write(directory, manifest, packer.DefaultNix(), directory);

            Assert.That(Directory.EnumerateFiles(directory, "*.dll"), Is.Empty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void Write_copies_only_the_module_files_that_exist()
    {
        var directory = CreateDirectory();
        var modules = CreateDirectory();
        try
        {
            var packer = new DotnetCapabilityPacker();
            var manifest = packer.Manifest();
            File.WriteAllText(Path.Join(modules, manifest.Module), "module");

            new DotnetCapabilityPackageWriter().Write(directory, manifest, packer.DefaultNix(), modules);

            Assert.That(
                Directory.EnumerateFiles(directory, "*.dll").Select(Path.GetFileName),
                Is.EqualTo(new[] { manifest.Module }));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
            Directory.Delete(modules, recursive: true);
        }
    }

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-dotnet-pack-" + Guid.NewGuid().ToString("N"))).FullName;
}
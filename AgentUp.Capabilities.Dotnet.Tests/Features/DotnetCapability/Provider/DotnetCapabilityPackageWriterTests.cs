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

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-dotnet-pack-" + Guid.NewGuid().ToString("N"))).FullName;
}

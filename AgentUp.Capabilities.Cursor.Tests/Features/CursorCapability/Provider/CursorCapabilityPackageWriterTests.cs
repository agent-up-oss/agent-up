using AgentUp.Capabilities.Cursor.Features.CursorCapability.Providers;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

namespace AgentUp.Capabilities.Cursor.Tests.Features.CursorCapability.Provider;

[TestFixture]
public sealed class CursorCapabilityPackageWriterTests
{
    [Test]
    public void Write_creates_capability_json()
    {
        var directory = CreateDirectory();
        try
        {
            var packer = new CursorCapabilityPacker();
            new CursorCapabilityPackageWriter().Write(directory, packer.Manifest(), packer.DefaultNix());
            Assert.That(File.Exists(Path.Join(directory, "capability.json")), Is.True);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public void Write_creates_default_nix()
    {
        var directory = CreateDirectory();
        try
        {
            var packer = new CursorCapabilityPacker();
            new CursorCapabilityPackageWriter().Write(directory, packer.Manifest(), packer.DefaultNix());
            Assert.That(File.ReadAllText(Path.Join(directory, "default.nix")), Does.Contain("nodejs"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-cursor-write-" + Guid.NewGuid().ToString("N"))).FullName;
}

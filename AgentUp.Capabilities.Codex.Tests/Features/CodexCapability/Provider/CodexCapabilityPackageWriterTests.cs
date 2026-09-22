using AgentUp.Capabilities.Codex.Features.CodexCapability.Providers;
using AgentUp.Capabilities.Codex.Features.CodexCapability.Services;

namespace AgentUp.Capabilities.Codex.Tests.Features.CodexCapability.Provider;

[TestFixture]
public sealed class CodexCapabilityPackageWriterTests
{
    [Test]
    public void Write_creates_capability_json()
    {
        var directory = CreateDirectory();
        try
        {
            var packer = new CodexCapabilityPacker();
            new CodexCapabilityPackageWriter().Write(directory, packer.Manifest(), packer.DefaultNix());
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
            var packer = new CodexCapabilityPacker();
            new CodexCapabilityPackageWriter().Write(directory, packer.Manifest(), packer.DefaultNix());
            Assert.That(File.ReadAllText(Path.Join(directory, "default.nix")), Does.Contain("nodejs"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-codex-write-" + Guid.NewGuid().ToString("N"))).FullName;
}

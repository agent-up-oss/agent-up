using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Providers;
using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Services;

namespace AgentUp.Capabilities.Claude.Tests.Features.ClaudeCapability.Provider;

[TestFixture]
public sealed class ClaudeCapabilityPackageWriterTests
{
    [Test]
    public void Write_creates_capability_json()
    {
        var directory = CreateDirectory();
        try
        {
            var packer = new ClaudeCapabilityPacker();
            new ClaudeCapabilityPackageWriter().Write(directory, packer.Manifest(), packer.DefaultNix());
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
            var packer = new ClaudeCapabilityPacker();
            new ClaudeCapabilityPackageWriter().Write(directory, packer.Manifest(), packer.DefaultNix());
            Assert.That(File.ReadAllText(Path.Join(directory, "default.nix")), Does.Contain("nodejs"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-claude-write-" + Guid.NewGuid().ToString("N"))).FullName;
}

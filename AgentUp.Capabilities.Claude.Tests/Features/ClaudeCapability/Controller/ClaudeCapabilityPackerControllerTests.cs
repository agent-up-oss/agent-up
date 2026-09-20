using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Controllers;
using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Providers;
using AgentUp.Capabilities.Claude.Features.ClaudeCapability.Services;

namespace AgentUp.Capabilities.Claude.Tests.Features.ClaudeCapability.Controller;

[TestFixture]
public sealed class ClaudeCapabilityPackerControllerTests
{
    [Test]
    public void Pack_writes_capability_json()
    {
        var directory = CreateDirectory();
        try
        {
            new ClaudeCapabilityPackerController(
                new ClaudeCapabilityPackerService(new ClaudeCapabilityPacker(), new ClaudeCapabilityPackageWriter()))
                .Pack(directory);
            Assert.That(File.Exists(Path.Join(directory, "capability.json")), Is.True);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public void Pack_writes_default_nix()
    {
        var directory = CreateDirectory();
        try
        {
            new ClaudeCapabilityPackerController(
                new ClaudeCapabilityPackerService(new ClaudeCapabilityPacker(), new ClaudeCapabilityPackageWriter()))
                .Pack(directory);
            Assert.That(File.Exists(Path.Join(directory, "default.nix")), Is.True);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-claude-pack-" + Guid.NewGuid().ToString("N"))).FullName;
}

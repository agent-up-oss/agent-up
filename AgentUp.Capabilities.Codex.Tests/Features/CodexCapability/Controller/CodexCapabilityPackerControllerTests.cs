using AgentUp.Capabilities.Codex.Features.CodexCapability.Controllers;
using AgentUp.Capabilities.Codex.Features.CodexCapability.Providers;
using AgentUp.Capabilities.Codex.Features.CodexCapability.Services;

namespace AgentUp.Capabilities.Codex.Tests.Features.CodexCapability.Controller;

[TestFixture]
public sealed class CodexCapabilityPackerControllerTests
{
    [Test]
    public void Pack_writes_capability_json()
    {
        var directory = CreateDirectory();
        try
        {
            new CodexCapabilityPackerController(
                new CodexCapabilityPackerService(new CodexCapabilityPacker(), new CodexCapabilityPackageWriter()))
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
            new CodexCapabilityPackerController(
                new CodexCapabilityPackerService(new CodexCapabilityPacker(), new CodexCapabilityPackageWriter()))
                .Pack(directory);
            Assert.That(File.Exists(Path.Join(directory, "default.nix")), Is.True);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-codex-pack-" + Guid.NewGuid().ToString("N"))).FullName;
}

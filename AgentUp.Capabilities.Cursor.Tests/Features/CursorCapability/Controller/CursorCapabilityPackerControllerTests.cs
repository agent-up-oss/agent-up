using AgentUp.Capabilities.Cursor.Features.CursorCapability.Controllers;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Providers;
using AgentUp.Capabilities.Cursor.Features.CursorCapability.Services;

namespace AgentUp.Capabilities.Cursor.Tests.Features.CursorCapability.Controller;

[TestFixture]
public sealed class CursorCapabilityPackerControllerTests
{
    [Test]
    public void Pack_writes_capability_json()
    {
        var directory = CreateDirectory();
        try
        {
            new CursorCapabilityPackerController(
                new CursorCapabilityPackerService(new CursorCapabilityPacker(), new CursorCapabilityPackageWriter()))
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
            new CursorCapabilityPackerController(
                new CursorCapabilityPackerService(new CursorCapabilityPacker(), new CursorCapabilityPackageWriter()))
                .Pack(directory);
            Assert.That(File.Exists(Path.Join(directory, "default.nix")), Is.True);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string CreateDirectory()
        => Directory.CreateDirectory(Path.Join(Path.GetTempPath(), "agent-up-cursor-pack-" + Guid.NewGuid().ToString("N"))).FullName;
}

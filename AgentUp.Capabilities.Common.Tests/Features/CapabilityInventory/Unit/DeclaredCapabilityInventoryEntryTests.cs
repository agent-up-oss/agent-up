using AgentUp.Capabilities.Common.Features.CapabilityInventory.Models;

namespace AgentUp.Capabilities.Common.Tests.Features.CapabilityInventory.Unit;

[TestFixture]
public sealed class DeclaredCapabilityInventoryEntryTests
{
    [Test]
    public void Minimal_entry_leaves_optional_launch_overrides_unset()
    {
        var entry = new DeclaredCapabilityInventoryEntry("dotnet", ["10.0"]);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Id, Is.EqualTo("dotnet"));
            Assert.That(entry.Versions, Is.EqualTo(new[] { "10.0" }));
            Assert.That(entry.Command, Is.Null);
            Assert.That(entry.Arguments, Is.Null);
            Assert.That(entry.VersionArguments, Is.Null);
        });
    }

    [Test]
    public void Launch_overrides_are_preserved_without_rewriting_arguments()
    {
        var entry = new DeclaredCapabilityInventoryEntry(
            "codex", ["latest"], "/opt/codex", ["acp", "--stdio"], ["--version"]);

        Assert.Multiple(() =>
        {
            Assert.That(entry.Command, Is.EqualTo("/opt/codex"));
            Assert.That(entry.Arguments, Is.EqualTo(new[] { "acp", "--stdio" }));
            Assert.That(entry.VersionArguments, Is.EqualTo(new[] { "--version" }));
        });
    }
}

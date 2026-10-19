using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Tests.Features.Verification.Unit;

[TestFixture]
public sealed class ToolchainVersionTests
{
    [Test]
    public void Read_pullsTheDottedVersionOutOfAToolBanner()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToolchainVersion.Read("git version 2.40.1"), Is.EqualTo("2.40.1"));
            Assert.That(ToolchainVersion.Read("v22.14.0"), Is.EqualTo("22.14.0"));
            Assert.That(ToolchainVersion.Read("Docker version 27.1.1, build abc"), Is.EqualTo("27.1.1"));
        });
    }

    [Test]
    public void Meets_comparesNumericComponents()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ToolchainVersion.Meets("8.0.100", "8.0.0"), Is.True);
            Assert.That(ToolchainVersion.Meets("6.0.0", "8.0.0"), Is.False);
            Assert.That(ToolchainVersion.Meets("10.0.0", "8.0.0"), Is.True);
            Assert.That(ToolchainVersion.Meets(null, "8.0.0"), Is.False);
            Assert.That(ToolchainVersion.Meets("8.0.0", "8.0.0"), Is.True);
            Assert.That(ToolchainVersion.Meets("99999999999.0", "1.0"), Is.False);
        });
    }
}

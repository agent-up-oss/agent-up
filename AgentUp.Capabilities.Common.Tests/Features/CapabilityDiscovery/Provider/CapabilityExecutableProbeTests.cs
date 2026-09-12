using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

namespace AgentUp.Capabilities.Common.Tests.Features.CapabilityDiscovery.Provider;

[TestFixture]
public sealed class CapabilityExecutableProbeTests
{
    [Test]
    public void IsExecutable_isFalseWhenThePathDoesNotExist()
    {
        Assert.That(new CapabilityExecutableProbe().IsExecutable(Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"))), Is.False);
    }

    [Test]
    public void IsExecutable_requiresTheUnixExecuteBit()
    {
        if (OperatingSystem.IsWindows())
            Assert.Ignore("Unix execute bits are not used on Windows.");

        var path = Path.GetTempFileName();
        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Assert.That(new CapabilityExecutableProbe().IsExecutable(path), Is.False);

            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Assert.That(new CapabilityExecutableProbe().IsExecutable(path), Is.True);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

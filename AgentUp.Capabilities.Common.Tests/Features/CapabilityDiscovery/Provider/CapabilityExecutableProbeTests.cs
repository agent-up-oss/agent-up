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
    public void IsExecutable_isTrueForAnExistingFileWhenTheHostTreatsFilesAsExecutable()
    {
        var path = Path.GetTempFileName();
        try
        {
            Assert.That(new CapabilityExecutableProbe(treatExistingFilesAsExecutable: true).IsExecutable(path), Is.True);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void IsExecutable_requiresTheUnixExecuteBitWhenTheHostHonorsUnixModes()
    {
        var path = Path.GetTempFileName();
        var modes = new Dictionary<string, UnixFileMode>(StringComparer.Ordinal)
        {
            [path] = UnixFileMode.UserRead | UnixFileMode.UserWrite
        };
        var probe = new CapabilityExecutableProbe(treatExistingFilesAsExecutable: false, unixFileMode: candidate => modes[candidate]);
        try
        {
            Assert.That(probe.IsExecutable(path), Is.False);

            modes[path] = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

            Assert.That(probe.IsExecutable(path), Is.True);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

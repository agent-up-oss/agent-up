using AgentUp.Verification.Features.Verification.Providers;

namespace AgentUp.Verification.Tests.Features.Verification.Provider;

[TestFixture]
public sealed class ProcessToolchainProbeTests
{
    [Test]
    public void Probe_readsTheVersionPrintedByAnExecutableOnTheSuppliedPath()
    {
        var directory = Path.Join(TestContext.CurrentContext.WorkDirectory, "probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var executable = Path.Join(directory, "git");
        File.WriteAllText(executable, "#!/bin/sh\nprintf 'git version 2.40.1\\n'\n");
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        var observation = new ProcessToolchainProbe().Probe("git", directory);

        Assert.Multiple(() =>
        {
            Assert.That(observation.Present, Is.True);
            Assert.That(observation.Version, Is.EqualTo("2.40.1"));
        });
    }

    [Test]
    public void Probe_reportsAnUnknownIdAsMissingRatherThanSearchingThePath()
    {
        var observation = new ProcessToolchainProbe().Probe("make", "/usr/bin");

        Assert.That(observation.Present, Is.False);
    }
}

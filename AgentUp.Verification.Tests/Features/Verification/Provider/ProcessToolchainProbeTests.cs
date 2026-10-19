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
        var observation = new ProcessToolchainProbe().Probe("make");

        Assert.That(observation.Present, Is.False);
    }

    [Test]
    public void Probe_reportsMissingWhenThePathIsBlankOrDoesNotContainTheTool()
    {
        var directory = Path.Join(TestContext.CurrentContext.WorkDirectory, "probe-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var probe = new ProcessToolchainProbe();

        Assert.Multiple(() =>
        {
            Assert.That(probe.Probe("git", "   ").Present, Is.False);
            Assert.That(probe.Probe("git", null).Present, Is.False);
            Assert.That(probe.Probe("git", directory).Present, Is.False);
        });
    }

    [Test]
    public void Probe_readsStderrAndKeepsAMultiLineBanner()
    {
        var directory = Script("git", "#!/bin/sh\nprintf 'line one\\nline two\\n'\nprintf 'git version 2.40.1\\n' >&2\n");

        var observation = new ProcessToolchainProbe().Probe("git", directory);

        Assert.Multiple(() =>
        {
            Assert.That(observation.Present, Is.True);
            Assert.That(observation.Version, Is.EqualTo("2.40.1"));
        });
    }

    [Test]
    public void Probe_reportsMissingWhenTheToolExitsWithoutAVersion()
    {
        var directory = Script("git", "#!/bin/sh\nprintf 'nope\\n'\nexit 1\n");

        Assert.That(new ProcessToolchainProbe().Probe("git", directory).Present, Is.False);
    }

    [Test]
    public void Probe_reportsMissingWhenTheExecutableCannotStart()
    {
        var directory = Path.Join(TestContext.CurrentContext.WorkDirectory, "probe-bad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Join(directory, "git"), "not a program");

        Assert.That(new ProcessToolchainProbe().Probe("git", directory).Present, Is.False);
    }

    [Test]
    public void Probe_reportsMissingWhenTheToolDoesNotExit()
    {
        var directory = Script("git", "#!/bin/sh\nsleep 30\n");

        Assert.That(new ProcessToolchainProbe().Probe("git", directory).Present, Is.False);
    }

    private static string Script(string name, string body)
    {
        var directory = Path.Join(TestContext.CurrentContext.WorkDirectory, "probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var executable = Path.Join(directory, name);
        File.WriteAllText(executable, body);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(executable, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        return directory;
    }
}

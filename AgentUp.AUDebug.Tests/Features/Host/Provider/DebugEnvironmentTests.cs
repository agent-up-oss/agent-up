using AgentUp.AUDebug.Shared.Providers;

namespace AgentUp.AUDebug.Tests.Features.Host.Provider;

[TestFixture]
public sealed class DebugEnvironmentTests
{
    [Test]
    public void Display_defaultsToColonZero()
    {
        var previous = Environment.GetEnvironmentVariable("DISPLAY");
        try
        {
            Environment.SetEnvironmentVariable("DISPLAY", null);
            Assert.That(new DebugEnvironment().Display, Is.EqualTo(":0"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("DISPLAY", previous);
        }
    }

    [Test]
    public void FindOnPath_emptyPath_isNull()
    {
        var previous = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", "");
            Assert.That(new DebugEnvironment().FindOnPath("xdotool"), Is.Null);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previous);
        }
    }

    [Test]
    public void FindOnPath_returnsExistingFile()
    {
        var directory = Path.Join(Path.GetTempPath(), "au-debug-env", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var executable = Path.Join(directory, "xdotool");
        File.WriteAllText(executable, string.Empty);
        var previous = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", directory);
            Assert.That(new DebugEnvironment().FindOnPath("xdotool"), Is.EqualTo(executable));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previous);
        }
    }

    [Test]
    public void AdminPassword_readsEnvironment()
    {
        var previous = Environment.GetEnvironmentVariable("AGENTUP_ADMIN_PASSWORD");
        try
        {
            Environment.SetEnvironmentVariable("AGENTUP_ADMIN_PASSWORD", "from-env");
            Assert.That(new DebugEnvironment().AdminPassword, Is.EqualTo("from-env"));
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTUP_ADMIN_PASSWORD", previous);
        }
    }

    [Test]
    public void FindChromium_skipsShellWrapper()
    {
        var directory = Path.Join(Path.GetTempPath(), "au-debug-chrome", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Join(directory, "google-chrome"), "#!/bin/bash\nexec true --remote-debugging-port=9222\n");
        var stable = Path.Join(directory, "google-chrome-stable");
        File.WriteAllText(stable, "#!/bin/bash\nexec chrome \"$@\"\n");
        var previous = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", directory);
            Assert.That(new DebugEnvironment().FindChromium(), Is.EqualTo(stable));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previous);
        }
    }
}

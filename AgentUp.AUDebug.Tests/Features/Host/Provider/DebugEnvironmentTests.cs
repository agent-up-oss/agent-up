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

    [Test]
    public void FindChromium_missing_isNull()
    {
        var previous = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", Path.Join(Path.GetTempPath(), "au-debug-no-chrome", Guid.NewGuid().ToString("N")));
            Assert.That(new DebugEnvironment().FindChromium(), Is.Null);
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previous);
        }
    }

    [Test]
    public void FindChromium_acceptsBinaryExecutable()
    {
        var directory = Path.Join(Path.GetTempPath(), "au-debug-chrome-bin", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var chromium = Path.Join(directory, "chromium");
        File.WriteAllBytes(chromium, [0x7F, 0x45, 0x4C, 0x46]);
        var previous = Environment.GetEnvironmentVariable("PATH");
        try
        {
            Environment.SetEnvironmentVariable("PATH", directory);
            Assert.That(new DebugEnvironment().FindChromium(), Is.EqualTo(chromium));
        }
        finally
        {
            Environment.SetEnvironmentVariable("PATH", previous);
        }
    }

    [Test]
    public void FindChromium_skipsUnreadableWrapper()
    {
        var directory = Path.Join(Path.GetTempPath(), "au-debug-chrome-sock", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var chromium = Path.Join(directory, "chromium");
        using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.Unix, System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Unspecified);
        socket.Bind(new System.Net.Sockets.UnixDomainSocketEndPoint(chromium));
        var stable = Path.Join(directory, "google-chrome-stable");
        File.WriteAllBytes(stable, [0x7F, 0x45]);
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

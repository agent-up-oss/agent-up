using AgentUp.InstallerApp.Features.Logging.Tools;

namespace AgentUp.InstallerApp.Tests.Features.Logging.Provider;

[TestFixture]
public class InstallerLogTests
{
    [Test]
    public void ResolveMacOsLogPath_whenPrivileged_isSystemLogPath()
    {
        Assert.That(
            InstallerLog.ResolveMacOsLogPath(isPrivileged: true, systemDirExists: false),
            Is.EqualTo("/Library/Logs/Agent-Up/installer.log"));
    }

    [Test]
    public void ResolveMacOsLogPath_whenSystemDirExists_isSystemLogPath()
    {
        Assert.That(
            InstallerLog.ResolveMacOsLogPath(isPrivileged: false, systemDirExists: true),
            Is.EqualTo("/Library/Logs/Agent-Up/installer.log"));
    }

    [Test]
    public void ResolveMacOsLogPath_whenUnprivilegedAndSystemDirAbsent_isUserLogPath()
    {
        var path = InstallerLog.ResolveMacOsLogPath(isPrivileged: false, systemDirExists: false);
        Assert.That(path, Does.StartWith(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)),
            "Must fall back to the user home directory when non-root and system dir absent");
        Assert.That(path, Does.Contain("Agent-Up"));
        Assert.That(path, Does.EndWith("installer.log"));
        Assert.That(path, Does.Not.StartWith("/Library/Logs"),
            "Must not use system path — non-root cannot create /Library/Logs/Agent-Up/");
    }

    [Test]
    public void FilePath_isFullyQualifiedAgentUpInstallerLogPath()
    {
        Assert.That(InstallerLog.FilePath, Does.Contain("agent-up").IgnoreCase);
        Assert.That(InstallerLog.FilePath, Does.EndWith("installer.log"));
        Assert.That(Path.IsPathFullyQualified(InstallerLog.FilePath), Is.True);
    }

    [Test]
    public void Write_neverThrows_regardlessOfPermissions()
    {
        // The catch-all in Write is critical: logging must never crash the installer.
        // This verifies the contract holds even when the path may not be writable (e.g. non-root on macOS).
        Assert.DoesNotThrow(() => InstallerLog.Write("smoke test: Write should never throw"));
    }

    [Test]
    public void WriteException_neverThrows()
    {
        Assert.DoesNotThrow(() => InstallerLog.WriteException("smoke test", new InvalidOperationException("test error")));
    }

    [Test]
    public void Write_appendsMessageToFile_whenPathIsWritable()
    {
        var tempDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var logPath = Path.Join(tempDir, "test-installer.log");
        try
        {
            InstallerLog.WriteToPath(logPath, "first message");
            InstallerLog.WriteToPath(logPath, "second message");

            var contents = File.ReadAllText(logPath);
            Assert.That(contents, Does.Contain("first message"));
            Assert.That(contents, Does.Contain("second message"));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }

    [Test]
    [Platform(Exclude = "Win")]
    public void Write_setsWorldReadWritePermissionsOnNewFile_whenPathIsWritable()
    {
        var tempDir = Path.Join(Path.GetTempPath(), Path.GetRandomFileName());
        var logPath = Path.Join(tempDir, "test-installer.log");
        try
        {
            InstallerLog.WriteToPath(logPath, "permission check");

#pragma warning disable CA1416
            var fileMode = File.GetUnixFileMode(logPath);
            Assert.That(fileMode & UnixFileMode.OtherRead, Is.EqualTo(UnixFileMode.OtherRead),
                "Log file must be other-readable so non-root users can read logs created by root");
            Assert.That(fileMode & UnixFileMode.OtherWrite, Is.EqualTo(UnixFileMode.OtherWrite),
                "Log file must be other-writable so non-root GUI process can append after root --install-core created it");

            var dirMode = File.GetUnixFileMode(tempDir);
            Assert.That(dirMode & UnixFileMode.OtherWrite, Is.EqualTo(UnixFileMode.OtherWrite),
                "Log directory must be other-writable");
#pragma warning restore CA1416
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}

using AgentUp.InstallerApp.Features.Logging;

namespace AgentUp.InstallerApp.Tests.Features.Logging;

[TestFixture]
public class InstallerLogTests
{
    [Test]
    public void FilePath_onMacOs_isSystemLogPath()
    {
        Assume.That(OperatingSystem.IsMacOS(), Is.True, "macOS path assertion only applies on macOS");
        Assert.That(InstallerLog.FilePath, Is.EqualTo("/Library/Logs/Agent-Up/installer.log"),
            "Must use a fixed system path so the root PKG postinstall process and the user GUI process both write to the same file. ~/Library would give root a different path.");
    }

    [Test]
    public void FilePath_onWindows_isLocalAppDataPath()
    {
        Assume.That(OperatingSystem.IsWindows(), Is.True, "Windows path assertion only applies on Windows");
        Assert.That(InstallerLog.FilePath, Does.Contain("Agent-Up"));
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
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var logPath = Path.Combine(tempDir, "test-installer.log");
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
        var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var logPath = Path.Combine(tempDir, "test-installer.log");
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

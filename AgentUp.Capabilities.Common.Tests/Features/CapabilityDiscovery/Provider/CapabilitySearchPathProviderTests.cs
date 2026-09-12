using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

namespace AgentUp.Capabilities.Common.Tests.Features.CapabilityDiscovery.Provider;

[TestFixture]
public sealed class CapabilitySearchPathProviderTests
{
    [Test]
    public void Directories_includeLocalBinAndHomebrewOnMac()
    {
        var directories = new CapabilitySearchPathProvider("/Users/dev", "macos", []).Directories();

        Assert.That(directories, Does.Contain("/Users/dev/.local/bin"));
        Assert.That(directories, Does.Contain("/opt/homebrew/bin"));
        Assert.That(directories, Does.Contain("/usr/local/bin"));
    }

    [Test]
    public void Directories_includeWindowsNpmShims()
    {
        var directories = new CapabilitySearchPathProvider(@"C:\Users\dev", "windows", []).Directories();

        Assert.That(directories, Does.Contain(Path.Join(@"C:\Users\dev", "AppData", "Roaming", "npm")));
    }

    [Test]
    public void Directories_appendExtraSearchRoots()
    {
        var extra = Path.Join(Path.GetTempPath(), "agent-tools");
        var directories = new CapabilitySearchPathProvider("/home/dev", "ubuntu", [extra]).Directories();

        Assert.That(directories, Does.Contain(extra));
        Assert.That(directories, Does.Not.Contain("/opt/homebrew/bin"));
    }

    [Test]
    public void Directories_includeCursorAgentVersionInstalls()
    {
        var home = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var version = Path.Join(home, ".local", "share", "cursor-agent", "versions", "2026.09.02-c22c1a3");
        Directory.CreateDirectory(version);
        Directory.CreateDirectory(Path.Join(home, ".local", "share", "cursor-agent", "versions", ".tmp-download"));
        try
        {
            var directories = new CapabilitySearchPathProvider(home, "ubuntu", []).Directories();
            Assert.That(directories, Does.Contain(version));
            Assert.That(directories, Does.Not.Contain(Path.Join(home, ".local", "share", "cursor-agent", "versions", ".tmp-download")));
        }
        finally
        {
            Directory.Delete(home, recursive: true);
        }
    }

    [Test]
    public void Directories_skipCursorAgentVersionsWhenTheRootIsUnreadable()
    {
        if (OperatingSystem.IsWindows())
            Assert.Ignore("Unix directory modes are not used on Windows.");

        var home = Path.Join(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var versionsRoot = Path.Join(home, ".local", "share", "cursor-agent", "versions");
        Directory.CreateDirectory(versionsRoot);
        File.SetUnixFileMode(versionsRoot, 0);
        try
        {
            var directories = new CapabilitySearchPathProvider(home, "ubuntu", []).Directories();
            Assert.That(directories, Does.Not.Contain(versionsRoot));
        }
        finally
        {
            File.SetUnixFileMode(versionsRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(home, recursive: true);
        }
    }

    [Test]
    public void CurrentPlatform_matchesThisOperatingSystem()
    {
        var platform = CapabilitySearchPathProvider.CurrentPlatform();
        if (OperatingSystem.IsLinux())
            Assert.That(platform, Is.EqualTo("ubuntu"));
        else if (OperatingSystem.IsMacOS())
            Assert.That(platform, Is.EqualTo("macos"));
        else if (OperatingSystem.IsWindows())
            Assert.That(platform, Is.EqualTo("windows"));
        else
            Assert.That(platform, Is.EqualTo("unknown"));
    }
}

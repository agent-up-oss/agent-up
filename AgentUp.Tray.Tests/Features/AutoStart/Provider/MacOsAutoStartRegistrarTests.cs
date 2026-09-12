using System.Xml.Linq;
using AgentUp.Tray.Features.AutoStart;
using AgentUp.Tray.Tests.Support;

namespace AgentUp.Tray.Tests.Features.AutoStart.Provider;

[TestFixture]
public sealed class MacOsAutoStartRegistrarTests
{
    private const string TrayBinary = "/Applications/Agent-Up.app/Contents/MacOS/AgentUp.Tray";
    private const string PlistName = "dev.agent-up.tray.plist";

    private static MacOsAutoStartRegistrar Registrar(string directory, List<string>? launchctl = null)
        => new(TrayBinary, directory, (verb, path) => launchctl?.Add($"{verb} {Path.GetFileName(path)}"));

    [Test]
    public void IsRegistered_isFalseBeforeRegistering()
    {
        using var directory = AutostartDirectory.Create();

        Assert.That(Registrar(directory.Path).IsRegistered(), Is.False);
    }

    [Test]
    public void Register_writesTheLaunchAgentPlist()
    {
        using var directory = AutostartDirectory.Create();
        var registrar = Registrar(directory.Path);

        registrar.Register();

        Assert.Multiple(() =>
        {
            Assert.That(registrar.IsRegistered(), Is.True);
            Assert.That(File.Exists(directory.FileAt(PlistName)), Is.True);
        });
    }

    [Test]
    public void Register_writesTheKeysLaunchdNeedsToKeepTheTrayRunning()
    {
        using var directory = AutostartDirectory.Create();
        Registrar(directory.Path).Register();

        var plist = XDocument.Parse(File.ReadAllText(directory.FileAt(PlistName)));
        var keys = plist.Descendants("key").Select(key => key.Value).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(keys, Does.Contain("Label"));
            Assert.That(keys, Does.Contain("ProgramArguments"));
            Assert.That(keys, Does.Contain("RunAtLoad"));
            Assert.That(keys, Does.Contain("KeepAlive"));
            Assert.That(keys, Does.Contain("ThrottleInterval"));
        });
    }

    [Test]
    public void Register_pointsLaunchdAtTheTrayBinary()
    {
        using var directory = AutostartDirectory.Create();
        Registrar(directory.Path).Register();

        var plist = XDocument.Parse(File.ReadAllText(directory.FileAt(PlistName)));
        var arguments = plist.Descendants("array").Descendants("string").Select(s => s.Value).ToArray();

        Assert.That(arguments, Is.EqualTo(new[] { TrayBinary }));
    }

    [Test]
    public void Register_labelsTheAgentWithTheProductIdentifier()
    {
        using var directory = AutostartDirectory.Create();
        Registrar(directory.Path).Register();

        Assert.That(File.ReadAllText(directory.FileAt(PlistName)), Does.Contain("dev.agent-up.tray"));
    }

    [Test]
    public void Register_loadsTheAgentIntoLaunchdForTheCurrentSession()
    {
        using var directory = AutostartDirectory.Create();
        var launchctl = new List<string>();

        Registrar(directory.Path, launchctl).Register();

        Assert.That(launchctl, Is.EqualTo(new[] { $"load {PlistName}" }));
    }

    [Test]
    public void Register_createsTheLaunchAgentsDirectoryWhenItDoesNotExist()
    {
        using var directory = AutostartDirectory.Absent();

        Registrar(directory.Path).Register();

        Assert.That(File.Exists(directory.FileAt(PlistName)), Is.True);
    }

    [Test]
    public void Unregister_unloadsBeforeDeletingSoLaunchdDoesNotKeepAGhostJob()
    {
        using var directory = AutostartDirectory.Create();
        var launchctl = new List<string>();
        var registrar = Registrar(directory.Path, launchctl);
        registrar.Register();
        launchctl.Clear();

        registrar.Unregister();

        Assert.Multiple(() =>
        {
            Assert.That(launchctl, Is.EqualTo(new[] { $"unload {PlistName}" }));
            Assert.That(registrar.IsRegistered(), Is.False);
        });
    }

    [Test]
    public void Unregister_doesNotCallLaunchctlWhenNothingIsRegistered()
    {
        using var directory = AutostartDirectory.Create();
        var launchctl = new List<string>();

        Registrar(directory.Path, launchctl).Unregister();

        Assert.That(launchctl, Is.Empty);
    }

    [Test]
    public void PropertyList_isValidXmlWithAPlistRoot()
    {
        using var directory = AutostartDirectory.Create();

        var plist = XDocument.Parse(Registrar(directory.Path).PropertyList);

        Assert.That(plist.Root!.Name.LocalName, Is.EqualTo("plist"));
    }
}

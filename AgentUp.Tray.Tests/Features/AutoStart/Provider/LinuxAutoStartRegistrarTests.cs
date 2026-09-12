using AgentUp.Tray.Features.AutoStart;
using AgentUp.Tray.Tests.Support;

namespace AgentUp.Tray.Tests.Features.AutoStart.Provider;

[TestFixture]
public sealed class LinuxAutoStartRegistrarTests
{
    private const string TrayBinary = "/opt/agent-up/tray/AgentUp.Tray";
    private const string FileName = "agent-up-tray.desktop";

    [Test]
    public void IsRegistered_isFalseBeforeRegistering()
    {
        using var directory = AutostartDirectory.Create();

        Assert.That(new LinuxAutoStartRegistrar(TrayBinary, directory.Path).IsRegistered(), Is.False);
    }

    [Test]
    public void Register_writesTheXdgDesktopEntry()
    {
        using var directory = AutostartDirectory.Create();
        var registrar = new LinuxAutoStartRegistrar(TrayBinary, directory.Path);

        registrar.Register();

        Assert.Multiple(() =>
        {
            Assert.That(registrar.IsRegistered(), Is.True);
            Assert.That(File.Exists(directory.FileAt(FileName)), Is.True);
        });
    }

    [Test]
    public void Register_writesTheFieldsAnAutostartLauncherNeeds()
    {
        using var directory = AutostartDirectory.Create();
        var registrar = new LinuxAutoStartRegistrar(TrayBinary, directory.Path);

        registrar.Register();
        var entry = File.ReadAllText(directory.FileAt(FileName));

        Assert.Multiple(() =>
        {
            Assert.That(entry, Does.StartWith("[Desktop Entry]"));
            Assert.That(entry, Does.Contain("Type=Application"));
            Assert.That(entry, Does.Contain($"Exec={TrayBinary}"));
            Assert.That(entry, Does.Contain("Hidden=false"));
            Assert.That(entry, Does.Contain("X-GNOME-Autostart-enabled=true"),
                "Without this GNOME silently ignores the entry.");
        });
    }

    [Test]
    public void Register_createsTheAutostartDirectoryWhenItDoesNotExist()
    {
        using var directory = AutostartDirectory.Absent();

        new LinuxAutoStartRegistrar(TrayBinary, directory.Path).Register();

        Assert.That(File.Exists(directory.FileAt(FileName)), Is.True);
    }

    [Test]
    public void Register_isIdempotent()
    {
        using var directory = AutostartDirectory.Create();
        var registrar = new LinuxAutoStartRegistrar(TrayBinary, directory.Path);

        registrar.Register();
        registrar.Register();

        Assert.That(Directory.GetFiles(directory.Path), Has.Length.EqualTo(1));
    }

    [Test]
    public void Unregister_removesTheEntry()
    {
        using var directory = AutostartDirectory.Create();
        var registrar = new LinuxAutoStartRegistrar(TrayBinary, directory.Path);
        registrar.Register();

        registrar.Unregister();

        Assert.That(registrar.IsRegistered(), Is.False);
    }

    [Test]
    public void Unregister_isSafeWhenNothingIsRegistered()
    {
        using var directory = AutostartDirectory.Create();

        Assert.That(() => new LinuxAutoStartRegistrar(TrayBinary, directory.Path).Unregister(), Throws.Nothing);
    }

    [Test]
    public void EnsureRegistered_registersOnlyWhenAbsent()
    {
        using var directory = AutostartDirectory.Create();
        IAutoStartRegistrar registrar = new LinuxAutoStartRegistrar(TrayBinary, directory.Path);

        registrar.EnsureRegistered();
        var afterFirst = File.GetLastWriteTimeUtc(directory.FileAt(FileName));
        registrar.EnsureRegistered();

        Assert.That(File.GetLastWriteTimeUtc(directory.FileAt(FileName)), Is.EqualTo(afterFirst),
            "An already-registered entry must not be rewritten.");
    }

    [Test]
    public void DesktopEntry_isTheContentRegisterWrites()
    {
        // The property is what a caller inspects without touching the filesystem, so it has
        // to stay the same text Register persists.
        using var directory = AutostartDirectory.Create();
        var registrar = new LinuxAutoStartRegistrar(TrayBinary, directory.Path);

        registrar.Register();

        Assert.That(registrar.DesktopEntry, Is.EqualTo(File.ReadAllText(directory.FileAt(FileName))));
    }
}

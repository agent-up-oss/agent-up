using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Providers;

namespace AgentUp.Desktop.Tests.Features.Interface.Provider;

public sealed class FileInterfacePreferenceStoreTests
{
    [Test]
    public void A_client_with_no_stored_preference_opens_on_the_classic_shell()
    {
        var store = new FileInterfacePreferenceStore(Path.Join(NewDirectory(), "interface.json"));

        Assert.That(store.Load().Choice, Is.EqualTo(InterfaceChoice.Classic));
    }

    [Test]
    public void The_choice_survives_a_restart()
    {
        var path = Path.Join(NewDirectory(), "interface.json");
        new FileInterfacePreferenceStore(path).Save(new InterfacePreference(InterfaceChoice.CommandCenter));

        Assert.That(new FileInterfacePreferenceStore(path).Load().Choice, Is.EqualTo(InterfaceChoice.CommandCenter));
    }

    [Test]
    public void Saving_creates_the_directory_it_writes_into()
    {
        var path = Path.Join(NewDirectory(), "nested", "interface.json");

        Assert.That(new FileInterfacePreferenceStore(path).Save(new InterfacePreference(InterfaceChoice.CommandCenter)), Is.True);
    }

    [Test]
    public void An_unreadable_preference_falls_back_to_the_shell_that_predates_the_setting()
    {
        var path = Path.Join(NewDirectory(), "interface.json");
        File.WriteAllText(path, "{ not json");

        Assert.That(new FileInterfacePreferenceStore(path).Load().Choice, Is.EqualTo(InterfaceChoice.Classic));
    }

    [Test]
    public void A_preference_that_cannot_be_written_reports_that_rather_than_throwing()
    {
        var directory = NewDirectory();
        var path = Path.Join(directory, "interface.json");
        Directory.CreateDirectory(path);

        Assert.That(new FileInterfacePreferenceStore(path).Save(new InterfacePreference(InterfaceChoice.CommandCenter)), Is.False);
    }

    private static string NewDirectory()
    {
        var directory = Path.Join(Path.GetTempPath(), "agent-up-interface-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}

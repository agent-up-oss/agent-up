using AgentUp.Desktop.Composition;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.Providers;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

/// <summary>
/// The suite must not read the developer's own interface choice. It once did: every headless test
/// that builds the window fell back to the file store, so a machine where the command center had
/// been switched on failed twenty-six tests that assert on classic-shell controls.
/// </summary>
[TestFixture]
public sealed class InterfaceIsolationTests
{
    [Test]
    public void CreateWithACommandCenter_doesNotFallBackToTheFileStore()
    {
        var chosen = InterfaceTestComposition.CommandCenter();
        using var http = new HttpClient();

        var vm = MainViewModelFactory.Create(http, commandCenter: chosen);

        Assert.Multiple(() =>
        {
            Assert.That(vm.CommandCenter, Is.SameAs(chosen));
            Assert.That(vm.CommandCenter!.Active, Is.True);
        });
    }

    [Test]
    public void TheSharedTestComposition_startsClassicAndSurvivesARoundTrip()
    {
        var classic = InterfaceTestComposition.Classic();

        Assert.That(classic.Active, Is.False);

        classic.ToggleCommand.Execute().Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(classic.Active, Is.True);
            Assert.That(classic.Persists, Is.True, "the memory store reports a stored choice");
            Assert.That(InterfaceTestComposition.Classic().Active, Is.False, "each test gets its own store");
        });
    }

    [Test]
    public void TheMemoryStore_keepsWhatItWasGiven()
    {
        var store = new MemoryInterfacePreferenceStore();

        Assert.That(store.Load().Choice, Is.EqualTo(InterfaceChoice.Classic));
        Assert.That(store.Save(new InterfacePreference { Choice = InterfaceChoice.CommandCenter }), Is.True);
        Assert.That(store.Load().Choice, Is.EqualTo(InterfaceChoice.CommandCenter));
    }

    [Test]
    public void TheFileStore_isWhatProductionFallsBackTo()
    {
        Assert.That(new FileInterfacePreferenceStore(), Is.InstanceOf<FileInterfacePreferenceStore>(),
            "if this type goes away, the comment above about the fallback needs rewriting too");
    }
}

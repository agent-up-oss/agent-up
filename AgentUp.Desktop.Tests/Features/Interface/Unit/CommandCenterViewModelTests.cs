using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Interface.Unit;

public sealed class CommandCenterViewModelTests
{
    [Test]
    public void The_window_draws_the_classic_shell_until_the_command_center_is_chosen()
    {
        var view = new CommandCenterViewModel(new InterfaceController(new FakeStore(InterfaceChoice.Classic)));

        Assert.Multiple(() =>
        {
            Assert.That(view.Active, Is.False);
            Assert.That(view.Classic, Is.True);
        });
    }

    [Test]
    public void Toggling_swaps_which_shell_the_window_draws()
    {
        var view = new CommandCenterViewModel(new InterfaceController(new FakeStore(InterfaceChoice.Classic)));

        view.ToggleCommand.Execute().Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(view.Active, Is.True);
            Assert.That(view.Classic, Is.False);
        });
    }

    [Test]
    public void The_tooltip_names_the_shell_the_switch_leads_to()
    {
        var view = new CommandCenterViewModel(new InterfaceController(new FakeStore(InterfaceChoice.CommandCenter)));

        Assert.That(view.ToggleToolTip, Does.Contain("classic"));
    }

    [Test]
    public void A_machine_that_cannot_store_the_choice_still_switches_and_says_so()
    {
        var view = new CommandCenterViewModel(new InterfaceController(new FakeStore(InterfaceChoice.Classic, writable: false)));

        view.ToggleCommand.Execute().Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(view.Active, Is.True);
            Assert.That(view.Persists, Is.False);
        });
    }

    private sealed class FakeStore(InterfaceChoice choice, bool writable = true) : IInterfacePreferenceStore
    {
        public InterfacePreference Load() => new(choice);

        public bool Save(InterfacePreference preference) => writable;
    }
}

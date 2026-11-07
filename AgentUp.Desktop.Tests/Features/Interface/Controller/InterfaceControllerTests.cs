using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;

namespace AgentUp.Desktop.Tests.Features.Interface.Controller;

public sealed class InterfaceControllerTests
{
    [Test]
    public void The_controller_opens_on_whichever_shell_was_stored()
    {
        var controller = new InterfaceController(new FakeStore(InterfaceChoice.CommandCenter));

        Assert.That(controller.UsesCommandCenter, Is.True);
    }

    [Test]
    public void Toggling_moves_to_the_other_shell_and_stores_it()
    {
        var store = new FakeStore(InterfaceChoice.Classic);
        var controller = new InterfaceController(store);

        controller.Toggle();

        Assert.Multiple(() =>
        {
            Assert.That(controller.UsesCommandCenter, Is.True);
            Assert.That(store.Saved?.Choice, Is.EqualTo(InterfaceChoice.CommandCenter));
        });
    }

    [Test]
    public void The_command_center_can_always_be_left_again()
    {
        var controller = new InterfaceController(new FakeStore(InterfaceChoice.CommandCenter));

        controller.Toggle();

        Assert.That(controller.Choice, Is.EqualTo(InterfaceChoice.Classic));
    }

    [Test]
    public void A_choice_that_could_not_be_stored_is_reported_as_such()
    {
        var controller = new InterfaceController(new FakeStore(InterfaceChoice.Classic, writable: false));

        Assert.That(controller.Toggle(), Is.False);
    }

    private sealed class FakeStore(InterfaceChoice choice, bool writable = true) : IInterfacePreferenceStore
    {
        public InterfacePreference? Saved { get; private set; }

        public InterfacePreference Load() => new(choice);

        public bool Save(InterfacePreference preference)
        {
            if (!writable) return false;
            Saved = preference;
            return true;
        }
    }
}

using AgentUp.Desktop.Features.Interface.Controllers;
using AgentUp.Desktop.Features.Interface.Interfaces;
using AgentUp.Desktop.Features.Interface.Models;
using AgentUp.Desktop.Features.Interface.ViewModels;
using AgentUp.Desktop.Tests.Support;

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

    [Test]
    public void Opening_a_team_member_hands_the_pane_to_their_workspace()
    {
        var view = InterfaceTestComposition.CommandCenter();

        view.OpenAgentCommand.Execute(TeamStandingComposition.Standing()).Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(view.HasAgent, Is.True);
            Assert.That(view.AgentName, Is.EqualTo("Lucy"));
            Assert.That(view.Agent!.WorkspaceId, Is.EqualTo("lucy"));
            // The command center stops drawing its own surfaces so the workspace detail shows.
            Assert.That(view.ShowsOwnSurfaces, Is.False);
            Assert.That(view.ShowsSurfaces, Is.False);
        });
    }

    [Test]
    public void Closing_an_agent_hands_the_pane_back_to_the_surfaces()
    {
        var view = InterfaceTestComposition.CommandCenter();
        view.OpenAgentCommand.Execute(TeamStandingComposition.Standing()).Subscribe();

        view.CloseAgentCommand.Execute().Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(view.Agent, Is.Null);
            Assert.That(view.HasAgent, Is.False);
            Assert.That(view.ShowsOwnSurfaces, Is.True);
        });
    }

    [Test]
    public void Leaving_the_command_center_leaves_the_agent_it_had_open()
    {
        // Otherwise the classic shell comes back with its rail still stood down for a page that
        // is no longer on screen, and there is no way between workspaces at all.
        var view = InterfaceTestComposition.CommandCenter();
        view.OpenAgentCommand.Execute(TeamStandingComposition.Standing()).Subscribe();

        view.ToggleCommand.Execute().Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(view.Classic, Is.True);
            Assert.That(view.HasAgent, Is.False);
            Assert.That(view.ShowsOwnSurfaces, Is.False);
        });
    }

    [Test]
    public void The_classic_shell_draws_no_command_center_surfaces_even_with_no_agent_open()
    {
        var view = InterfaceTestComposition.Classic();

        Assert.Multiple(() =>
        {
            Assert.That(view.ShowsSurfaces, Is.True);
            Assert.That(view.ShowsOwnSurfaces, Is.False);
        });
    }

    private sealed class FakeStore(InterfaceChoice choice, bool writable = true) : IInterfacePreferenceStore
    {
        public InterfacePreference Load() => new(choice);

        public bool Save(InterfacePreference preference) => writable;
    }
}

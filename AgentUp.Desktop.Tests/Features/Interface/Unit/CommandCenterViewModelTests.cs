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
    public async Task Opening_a_team_member_puts_their_own_page_where_the_surfaces_were()
    {
        var workspaces = new AgentPageWorkspaceFake();
        workspaces.Workspaces["lucy"] = new WorkspaceDtoBuilder().Identified("lucy").AtCommit("a71f093").Build();
        var view = new CommandCenterViewModel(
            new InterfaceController(new FakeStore(InterfaceChoice.CommandCenter)),
            workspaces: AgentPageComposition.Workspaces(workspaces),
            git: AgentPageComposition.Git(new StandingForgeFake()));

        await view.OpenAgentAsync(TeamStandingComposition.Standing());

        Assert.Multiple(() =>
        {
            Assert.That(view.HasAgent, Is.True);
            Assert.That(view.ShowsSurfaces, Is.False);
            Assert.That(view.Agent!.DisplayName, Is.EqualTo("Lucy"));
            Assert.That(view.Agent.Facts.Single(fact => fact.Label == "Head").Value, Is.EqualTo("a71f093"));
        });
    }

    [Test]
    public async Task Closing_an_agent_hands_the_pane_back_to_the_surfaces()
    {
        var view = new CommandCenterViewModel(
            new InterfaceController(new FakeStore(InterfaceChoice.CommandCenter)),
            workspaces: AgentPageComposition.Workspaces(new AgentPageWorkspaceFake()),
            git: AgentPageComposition.Git(new StandingForgeFake()));
        await view.OpenAgentAsync(TeamStandingComposition.Standing());

        view.CloseAgentCommand.Execute().Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(view.Agent, Is.Null);
            Assert.That(view.HasAgent, Is.False);
            Assert.That(view.ShowsSurfaces, Is.True);
        });
    }

    [Test]
    public async Task A_shell_composed_without_the_reads_behind_a_page_simply_does_not_open_one()
    {
        // The headless window tests compose the shell without the two controllers. Opening a
        // member there has to stay a no-op rather than throw at a rail row's press.
        var view = InterfaceTestComposition.CommandCenter();

        await view.OpenAgentAsync(TeamStandingComposition.Standing());

        Assert.Multiple(() =>
        {
            Assert.That(view.Agent, Is.Null);
            Assert.That(view.ShowsSurfaces, Is.True);
        });
    }

    private sealed class FakeStore(InterfaceChoice choice, bool writable = true) : IInterfacePreferenceStore
    {
        public InterfacePreference Load() => new(choice);

        public bool Save(InterfacePreference preference) => writable;
    }
}

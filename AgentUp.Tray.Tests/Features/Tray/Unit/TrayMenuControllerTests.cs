using System.Net;
using Avalonia.Controls;
using AgentUp.Tray.Features.Tray;
using AgentUp.Tray.Tests.Features.Tray.Fake;

namespace AgentUp.Tray.Tests.Features.Tray.Unit;

[TestFixture]
public sealed class TrayMenuControllerTests
{
    [Test]
    public void Menu_showsTheStatusLineThenRestartThenQuit()
    {
        using var connection = new ServerConnectionManager(
            FakeHttpExchange.Answering(HttpStatusCode.OK).AsClient());
        using var controller = new TrayMenuController(connection, quit: () => { });

        Assert.That(Headers(controller.Menu), Is.EqualTo(new[] { "", "-", "Restart", "-", "Quit" }));
    }

    [Test]
    public void Menu_leavesTheStatusLineUnclickableBecauseItIsALabel()
    {
        using var connection = new ServerConnectionManager(
            FakeHttpExchange.Answering(HttpStatusCode.OK).AsClient());
        using var controller = new TrayMenuController(connection, quit: () => { });

        Assert.That(((NativeMenuItem)controller.Menu.Items[0]).IsEnabled, Is.False);
    }

    [Test]
    public void Restart_startsDisabledSoItCannotBeUsedBeforeTheStateIsKnown()
    {
        using var connection = new ServerConnectionManager(
            FakeHttpExchange.Answering(HttpStatusCode.OK).AsClient());
        using var controller = new TrayMenuController(connection, quit: () => { });

        Assert.That(((NativeMenuItem)controller.Menu.Items[2]).IsEnabled, Is.False);
    }

    [Test]
    public void ApplyState_putsTheStateInTheStatusLine()
    {
        using var connection = new ServerConnectionManager(
            FakeHttpExchange.Answering(HttpStatusCode.OK).AsClient());
        using var controller = new TrayMenuController(connection, quit: () => { });

        controller.ApplyState(ServiceState.Connected);

        Assert.That(((NativeMenuItem)controller.Menu.Items[0]).Header,
            Is.EqualTo(TrayStatusText.MenuLine(ServiceState.Connected)));
    }

    [Test]
    public void ApplyState_putsTheStateInTheTooltip()
    {
        using var connection = new ServerConnectionManager(
            FakeHttpExchange.Answering(HttpStatusCode.OK).AsClient());
        using var controller = new TrayMenuController(connection, quit: () => { });

        controller.ApplyState(ServiceState.Disconnected);

        Assert.That(controller.Icon.ToolTipText,
            Is.EqualTo(TrayStatusText.ToolTip(ServiceState.Disconnected)));
    }

    // Restart is offered only while the service is reachable: a disconnected service has
    // nothing to restart, and a restart already in flight must not be asked for twice.
    [TestCase(ServiceState.Connected, true)]
    [TestCase(ServiceState.Disconnected, false)]
    [TestCase(ServiceState.Connecting, false)]
    [TestCase(ServiceState.Restarting, false)]
    public void ApplyState_enablesRestartOnlyWhileTheServiceIsReachable(ServiceState state, bool enabled)
    {
        using var connection = new ServerConnectionManager(
            FakeHttpExchange.Answering(HttpStatusCode.OK).AsClient());
        using var controller = new TrayMenuController(connection, quit: () => { });

        controller.ApplyState(state);

        Assert.That(((NativeMenuItem)controller.Menu.Items[2]).IsEnabled, Is.EqualTo(enabled));
    }

    [Test]
    public void Icon_carriesTheProductTooltipBeforeAnyStateArrives()
    {
        using var connection = new ServerConnectionManager(
            FakeHttpExchange.Answering(HttpStatusCode.OK).AsClient());
        using var controller = new TrayMenuController(connection, quit: () => { });

        Assert.That(controller.Icon.ToolTipText, Is.EqualTo("Agent-Up"));
    }

    /// <summary>Renders the menu as display text, with "-" standing in for a separator.</summary>
    private static string[] Headers(NativeMenu menu)
        =>
        [
            .. menu.Items.Select(item => item switch
            {
                NativeMenuItemSeparator => "-",
                NativeMenuItem entry => entry.Header ?? string.Empty,
                _ => item.GetType().Name
            })
        ];
}

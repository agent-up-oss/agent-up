using AgentUp.Desktop.Features.Agents.Controllers;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Services;
using AgentUp.Desktop.Features.Agents.ViewModels;
using AgentUp.Desktop.Shared.Providers;
using AgentUp.Desktop.Tests.Features.Agents.Unit;
using AgentUp.Desktop.Tests.Support;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.NUnit;
using Avalonia.Threading;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;

namespace AgentUp.Desktop.Tests.Features.Agents.Headless;

[TestFixture]
public sealed class AgentLoopSurfaceTests
{
    [AvaloniaTest]
    public async Task LoopLine_rendersPhaseTaskAndStopReason()
    {
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(new ChatApiFake
        {
            Session = DesktopDomain.Session()
                .Ready()
                .BoundTo("Codex")
                .WithLoop(new AgentLoopStatusDto(
                    true,
                    "blocked",
                    TaskId: "2",
                    TaskNumber: 2,
                    TaskTitle: "Ship the review",
                    Terminal: "BlockedPolicy",
                    StopReason: "policy"))
                .Build()
        })));
        await view.LoadAsync("ws-1");

        var text = new TextBlock();
        text.Bind(TextBlock.TextProperty, new Binding(nameof(AgentChatViewModel.LoopLine)));
        var window = new Window { DataContext = view, Content = text };
        window.Show();
        await HeadlessExtensions.FlushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.HasLoop, Is.True);
            Assert.That(text.Text, Does.Contain("blocked"));
            Assert.That(text.Text, Does.Contain("Ship the review"));
            Assert.That(text.Text, Does.Contain("policy"));
        });
        await view.LoadAsync(null);
    }

    [AvaloniaTest]
    public async Task ChecklistAndControls_bindStableAutomationIds()
    {
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(new ChatApiFake
        {
            Autonomy = new(true, [
                new("forgeCapabilityEnabled", true),
                new("hostConnectionAuthorized", true),
                new("agentAvailable", true),
                new("autonomyEnabled", true)
            ]),
            Session = DesktopDomain.Session()
                .Ready()
                .BoundTo("Codex")
                .WithLoop(new AgentLoopStatusDto(false, "idle", Terminal: "Idle"))
                .Build()
        })));
        await view.LoadAsync("ws-1");

        var terminal = new TextBlock();
        terminal.Bind(TextBlock.TextProperty, new Binding(nameof(AgentChatViewModel.LoopTerminal)));
        var window = new Window { DataContext = view, Content = terminal };
        window.Show();
        await HeadlessExtensions.FlushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(view.Preconditions[3].AutomationId, Is.EqualTo(DesktopAutomationIds.AgentLoopPrecondition("autonomyEnabled")));
            Assert.That(view.CanActivateLoop, Is.True);
            Assert.That(terminal.Text, Is.EqualTo("Idle"));
        });
        await view.LoadAsync(null);
    }

    [AvaloniaTest]
    public async Task VisibleActiveLoop_pollsUntilTheTypedTerminalArrives()
    {
        var fake = new ChatApiFake
        {
            Autonomy = new(true, [
                new("forgeCapabilityEnabled", true),
                new("hostConnectionAuthorized", true),
                new("agentAvailable", true),
                new("autonomyEnabled", true)
            ]),
            Session = DesktopDomain.Session().Ready().BoundTo("Codex").Build(),
            IgnoreLoopReads = 1
        };
        fake.LoopPolls.Enqueue(new AgentLoopStatusDto(false, "idle", Terminal: "Completed", StopReason: "queue_empty"));
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(fake)));
        await view.LoadAsync("ws-1");
        view.IsVisible = true;

        await view.ActivateLoopCommand.Execute().FirstAsync();
        await WaitUntilAsync(() => view.LoopTerminal == "Completed");

        Assert.Multiple(() =>
        {
            Assert.That(view.HasLoopTerminal, Is.True);
            Assert.That(view.CanCancelLoop, Is.False);
            Assert.That(view.LoopPhase, Is.EqualTo("idle"));
        });
        view.IsVisible = false;
        await view.LoadAsync(null);
    }

    [AvaloniaTest]
    public async Task VisibleActiveLoop_retriesAfterATransientPollFailure()
    {
        var fake = new ChatApiFake
        {
            Autonomy = new(true, [
                new("forgeCapabilityEnabled", true),
                new("hostConnectionAuthorized", true),
                new("agentAvailable", true),
                new("autonomyEnabled", true)
            ]),
            Session = DesktopDomain.Session().Ready().BoundTo("Codex").Build(),
            IgnoreLoopReads = 1,
            LoopFailure = new HttpRequestException("loop down")
        };
        fake.LoopPolls.Enqueue(new AgentLoopStatusDto(false, "idle", Terminal: "Completed", StopReason: "queue_empty"));
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(fake)));
        await view.LoadAsync("ws-1");
        view.IsVisible = true;

        await view.ActivateLoopCommand.Execute().FirstAsync();
        await WaitUntilAsync(() => view.LoopTerminal == "Completed");

        Assert.That(view.LoopTerminal, Is.EqualTo("Completed"));
        view.IsVisible = false;
        await view.LoadAsync(null);
    }

    [AvaloniaTest]
    public async Task HidingTheSurface_stopsAnInFlightLoopPoll()
    {
        var hang = new TaskCompletionSource();
        var fake = new ChatApiFake
        {
            Autonomy = new(true, [
                new("forgeCapabilityEnabled", true),
                new("hostConnectionAuthorized", true),
                new("agentAvailable", true),
                new("autonomyEnabled", true)
            ]),
            Session = DesktopDomain.Session().Ready().BoundTo("Codex").Build(),
            IgnoreLoopReads = 1,
            LoopHang = hang
        };
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(fake)));
        await view.LoadAsync("ws-1");
        view.IsVisible = true;
        await view.ActivateLoopCommand.Execute().FirstAsync();

        view.IsVisible = false;
        hang.SetResult();
        await HeadlessExtensions.FlushAsync();

        Assert.That(view.Loop?.Active, Is.True);
        await view.LoadAsync(null);
    }

    [AvaloniaTest]
    public async Task VisibleActiveLoop_waitsBeforeTheNextPoll()
    {
        var fake = new ChatApiFake
        {
            Autonomy = new(true, [
                new("forgeCapabilityEnabled", true),
                new("hostConnectionAuthorized", true),
                new("agentAvailable", true),
                new("autonomyEnabled", true)
            ]),
            Session = DesktopDomain.Session().Ready().BoundTo("Codex").Build(),
            IgnoreLoopReads = 1
        };
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(fake)));
        await view.LoadAsync("ws-1");
        view.IsVisible = true;
        await view.ActivateLoopCommand.Execute().FirstAsync();
        await Task.Delay(80);

        view.IsVisible = false;
        await HeadlessExtensions.FlushAsync();

        Assert.That(view.Loop?.Active, Is.True);
        await view.LoadAsync(null);
    }

    [AvaloniaTest]
    public async Task HidingTheSurface_swallowsACancelledPollRead()
    {
        var fake = new ChatApiFake
        {
            Autonomy = new(true, [
                new("forgeCapabilityEnabled", true),
                new("hostConnectionAuthorized", true),
                new("agentAvailable", true),
                new("autonomyEnabled", true)
            ]),
            Session = DesktopDomain.Session().Ready().BoundTo("Codex").Build(),
            IgnoreLoopReads = 1,
            LoopFailure = new OperationCanceledException()
        };
        var view = new AgentChatViewModel(new AgentsController(new AgentChatService(fake)));
        await view.LoadAsync("ws-1");
        view.IsVisible = true;
        await view.ActivateLoopCommand.Execute().FirstAsync();
        await Task.Delay(50);

        view.IsVisible = false;
        await HeadlessExtensions.FlushAsync();

        Assert.That(view.Loop?.Active, Is.True);
        await view.LoadAsync(null);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Task.Delay(20, timeout.Token);
        }
    }
}

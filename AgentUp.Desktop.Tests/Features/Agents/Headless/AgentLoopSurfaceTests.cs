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
}

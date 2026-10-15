using AgentUp.Desktop.Features.Agents.Controllers;
using AgentUp.Desktop.Features.Agents.DTOs;
using AgentUp.Desktop.Features.Agents.Services;
using AgentUp.Desktop.Features.Agents.ViewModels;
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
    }
}

using System.Reactive;
using AgentUp.Desktop.Shared.Providers;
using ReactiveUI;
namespace AgentUp.Desktop.Features.Agents.ViewModels;
public sealed record AgentOptionViewModel(string Id, string Label, ReactiveCommand<Unit, Unit> Command)
{
    public string AutomationId => DesktopAutomationIds.AgentOption(Id);
}

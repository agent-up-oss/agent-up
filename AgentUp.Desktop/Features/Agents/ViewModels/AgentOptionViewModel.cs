using System.Reactive;
using ReactiveUI;
namespace AgentUp.Desktop.Features.Agents.ViewModels;
public sealed record AgentOptionViewModel(string Id, string Label, ReactiveCommand<Unit, Unit> Command);

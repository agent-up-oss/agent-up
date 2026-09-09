using System.Reactive;
using AgentUp.Desktop.Features.Validation.DTOs;
using ReactiveUI;
namespace AgentUp.Desktop.Features.Validation.ViewModels;
public sealed class ValidationFlowItemViewModel : ReactiveObject
{
    private bool _isRunning;
    public ValidationFlowDto Flow { get; }
    public string Id => Flow.Id; public string Name => Flow.Name; public string Description => Flow.Description; public string InitialPath => Flow.InitialPath; public int StepCount => Flow.Steps.Count;
    public bool IsRunning { get => _isRunning; private set => this.RaiseAndSetIfChanged(ref _isRunning, value); }
    public ReactiveCommand<Unit, Unit> RunCommand { get; }
    public ValidationFlowItemViewModel(ValidationFlowDto flow, Func<ValidationFlowDto, Task> run) { Flow = flow; RunCommand = ReactiveCommand.CreateFromTask(async () => { IsRunning = true; try { await run(flow); } finally { IsRunning = false; } }); }
}

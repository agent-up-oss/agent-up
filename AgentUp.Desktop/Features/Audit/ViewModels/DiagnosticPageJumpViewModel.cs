using System.Reactive;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Audit.ViewModels;

public sealed class DiagnosticPageJumpViewModel : ReactiveObject
{
    public DiagnosticPageJumpViewModel(int pageNumber, bool isCurrent, ReactiveCommand<Unit, Unit> jumpCommand)
    {
        PageNumber = pageNumber;
        IsCurrent = isCurrent;
        JumpCommand = jumpCommand;
    }

    public int PageNumber { get; }
    public bool IsCurrent { get; }
    public ReactiveCommand<Unit, Unit> JumpCommand { get; }
}

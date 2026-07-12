using ReactiveUI;

namespace AgentUp.Desktop.Features.Ports.ViewModels;

public abstract class SubTabViewModel : ReactiveObject
{
    public abstract string Label { get; }
}

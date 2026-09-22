using System.Reactive;
using AgentUp.Desktop.Features.Capabilities.DTOs;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Capabilities.ViewModels;

public sealed class CapabilityModuleCardViewModel : ReactiveObject
{
    public CapabilityModuleCardViewModel(CapabilityModuleDto module, CapabilityModulesViewModel catalog)
    {
        Id = module.Id;
        Version = module.Version;
        DisplayName = module.DisplayName;
        Publisher = module.Publisher;
        Enabled = module.Enabled;
        State = module.State;
        CanRun = module.CanRun;
        Summary = module.Messages.Count == 0 ? null : string.Join(" ", module.Messages);
        EnableCommand = ReactiveCommand.CreateFromTask(() => catalog.EnableAsync(Id, Version));
        DisableCommand = ReactiveCommand.CreateFromTask(() => catalog.DisableAsync(Id));
    }

    public string Id { get; }

    public string Version { get; }

    public string DisplayName { get; }

    public string Publisher { get; }

    public bool Enabled { get; }

    public string State { get; }

    public bool CanRun { get; }

    public string? Summary { get; }

    public string StatusLabel => CanRun ? "Ready" : State;

    public ReactiveCommand<Unit, Unit> EnableCommand { get; }

    public ReactiveCommand<Unit, Unit> DisableCommand { get; }
}

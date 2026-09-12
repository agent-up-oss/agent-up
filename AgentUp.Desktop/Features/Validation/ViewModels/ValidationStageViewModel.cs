using AgentUp.Desktop.Shared.Models;
using System.Collections.ObjectModel;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Models;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Validation.ViewModels;

public sealed class ValidationStageViewModel : ReactiveObject
{
    private ValidationRunState _state = ValidationRunState.Pending;

    public ValidationStageViewModel(string id, string title, IEnumerable<ValidationAssertionDto> checks)
    {
        Id = id;
        Title = title;
        Checks = new ObservableCollection<ValidationCheckViewModel>(
            checks.Select(check => new ValidationCheckViewModel(ValidationAssertionFormatting.Describe(check))));
    }

    public string Id { get; }
    public string Title { get; }
    public ObservableCollection<ValidationCheckViewModel> Checks { get; }

    public ValidationRunState State
    {
        get => _state;
        private set
        {
            this.RaiseAndSetIfChanged(ref _state, value);
            this.RaisePropertyChanged(nameof(StatusGlyph));
            this.RaisePropertyChanged(nameof(StatusColor));
        }
    }

    public string StatusGlyph => State switch
    {
        ValidationRunState.Passed => "✓",
        ValidationRunState.Failed => "✗",
        ValidationRunState.Running => "●",
        _ => "○"
    };

    public string StatusColor => State switch
    {
        ValidationRunState.Passed => AgentUpThemeColors.AccentSoft,
        ValidationRunState.Failed => AgentUpThemeColors.StatusDanger,
        ValidationRunState.Running => AgentUpThemeColors.StatusWarning,
        _ => AgentUpThemeColors.TextMuted
    };

    internal void Reset()
    {
        State = ValidationRunState.Pending;
        foreach (var check in Checks)
            check.Reset();
    }

    internal void SetRunning() => State = ValidationRunState.Running;

    internal void SetPassed() => State = ValidationRunState.Passed;

    internal void SetFailed() => State = ValidationRunState.Failed;

    internal ValidationCheckViewModel? GetCheck(int index)
        => index >= 0 && index < Checks.Count ? Checks[index] : null;
}

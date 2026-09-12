using AgentUp.Desktop.Shared.Models;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Models;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Validation.ViewModels;

public sealed class ValidationCheckViewModel : ReactiveObject
{
    private ValidationRunState _state = ValidationRunState.Pending;

    public ValidationCheckViewModel(string label) => Label = label;

    public string Label { get; }

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

    internal void Reset() => State = ValidationRunState.Pending;

    internal void SetRunning() => State = ValidationRunState.Running;

    internal void SetPassed() => State = ValidationRunState.Passed;

    internal void SetFailed() => State = ValidationRunState.Failed;
}

public static class ValidationAssertionFormatting
{
    public static string Describe(ValidationAssertionDto assertion) => assertion.Kind switch
    {
        ValidationExpectationDto.Text => $"Text contains \"{assertion.Value}\"",
        ValidationExpectationDto.Visible when assertion.Target?.Selector is { } selector => $"Visible: {selector}",
        ValidationExpectationDto.Url => $"URL is {assertion.Value}",
        ValidationExpectationDto.Title => $"Title is \"{assertion.Value}\"",
        _ => assertion.Value
    };
}

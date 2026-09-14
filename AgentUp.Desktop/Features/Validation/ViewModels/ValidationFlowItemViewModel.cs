using AgentUp.Desktop.Shared.Models;
using System.Collections.ObjectModel;
using System.Reactive;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Interfaces;
using AgentUp.Desktop.Features.Validation.Models;
using ReactiveUI;

namespace AgentUp.Desktop.Features.Validation.ViewModels;

public sealed class ValidationFlowItemViewModel : ReactiveObject, IValidationReplayReporter
{
    internal const string InitialStageId = "__initial__";

    private bool _isRunning;
    private bool _isExpanded;
    private ValidationRunState _runState = ValidationRunState.Pending;
    private string? _resultMessage;

    public ValidationFlowItemViewModel(ValidationFlowDto flow, Func<ValidationFlowItemViewModel, Task> run)
    {
        Flow = flow;
        Stages = new ObservableCollection<ValidationStageViewModel>(
        [
            new ValidationStageViewModel(InitialStageId, "Starting page", flow.InitialExpectations),
            ..flow.Steps.Select(step => new ValidationStageViewModel(
                step.Id,
                step.Description,
                step.Expectations ?? []))
        ]);
        RunCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            IsRunning = true;
            try
            {
                await run(this);
            }
            finally
            {
                IsRunning = false;
            }
        });
    }

    public ValidationFlowDto Flow { get; }
    public string Name => Flow.Name;
    public string Description => Flow.Description;
    public string InitialPath => Flow.InitialPath;
    public ObservableCollection<ValidationStageViewModel> Stages { get; }
    public ReactiveCommand<Unit, Unit> RunCommand { get; }

    public bool IsRunning
    {
        get => _isRunning;
        private set => this.RaiseAndSetIfChanged(ref _isRunning, value);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        private set => this.RaiseAndSetIfChanged(ref _isExpanded, value);
    }

    public ValidationRunState RunState
    {
        get => _runState;
        private set
        {
            this.RaiseAndSetIfChanged(ref _runState, value);
            this.RaisePropertyChanged(nameof(StatusGlyph));
            this.RaisePropertyChanged(nameof(StatusColor));
            this.RaisePropertyChanged(nameof(HasRunResult));
        }
    }

    public string? ResultMessage
    {
        get => _resultMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _resultMessage, value);
            this.RaisePropertyChanged(nameof(HasRunResult));
        }
    }

    public bool HasRunResult => RunState is ValidationRunState.Passed or ValidationRunState.Failed;

    public string StatusGlyph => RunState switch
    {
        ValidationRunState.Passed => "✓",
        ValidationRunState.Failed => "✗",
        ValidationRunState.Running => "●",
        _ => "○"
    };

    public string StatusColor => RunState switch
    {
        ValidationRunState.Passed => AgentUpThemeColors.AccentSoft,
        ValidationRunState.Failed => AgentUpThemeColors.StatusDanger,
        ValidationRunState.Running => AgentUpThemeColors.StatusWarning,
        _ => AgentUpThemeColors.TextMuted
    };

    public void BeginFlow()
    {
        IsExpanded = true;
        ResultMessage = null;
        RunState = ValidationRunState.Running;
        foreach (var stage in Stages)
            stage.Reset();
    }

    public void BeginStage(string stageId) => FindStage(stageId)?.SetRunning();

    public void BeginCheck(string stageId, int checkIndex) => FindStage(stageId)?.GetCheck(checkIndex)?.SetRunning();

    public void CompleteCheck(string stageId, int checkIndex, bool passed, string? error = null)
    {
        var check = FindStage(stageId)?.GetCheck(checkIndex);
        if (check is null)
            return;

        if (passed)
            check.SetPassed();
        else
            check.SetFailed();
    }

    public void CompleteStage(string stageId, bool passed, string? error = null)
    {
        var stage = FindStage(stageId);
        if (stage is null)
            return;

        if (passed)
            stage.SetPassed();
        else
            stage.SetFailed();
    }

    public void CompleteFlow(bool passed, string? message = null)
    {
        RunState = passed ? ValidationRunState.Passed : ValidationRunState.Failed;
        ResultMessage = message;
    }

    private ValidationStageViewModel? FindStage(string stageId)
        => Stages.FirstOrDefault(stage => string.Equals(stage.Id, stageId, StringComparison.Ordinal));
}

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AgentUp.Desktop.Features.Applications.ViewModels;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Interfaces;
using AgentUp.Desktop.Features.Validation.Models;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Features.Validation.Providers;

public sealed class AvaloniaValidationReplayHost(
    DesktopVisualTreeLocator locator,
    DesktopControlInput input,
    DesktopWindowCapture capture,
    DesktopSurfaceNavigator navigator) : IDesktopValidationReplayHost
{
    private Window? _window;
    private MainViewModel? _viewModel;
    private Control? _lastControl;

    public DesktopResolvedTargetDto? LastResolution { get; private set; }

    public IReadOnlyList<DesktopTargetCandidateDto> LastCandidates { get; private set; } = [];

    public void Attach(object window, object viewModel)
    {
        _window = (Window)window;
        _viewModel = (MainViewModel)viewModel;
        LastResolution = null;
        LastCandidates = [];
        _lastControl = null;
    }

    public async Task<string?> PrepareAsync(ValidationFlowDto flow, CancellationToken cancellationToken)
    {
        var error = await OnUi(() => Navigate(flow.InitialPath), cancellationToken);
        await FlushAsync();
        return error;
    }

    public async Task<string?> ExecuteAsync(ValidationStepDto step, CancellationToken cancellationToken)
    {
        var error = await OnUi(() => ExecuteOnUi(step), cancellationToken);
        await FlushAsync();
        return error;
    }

    public async Task<ValidationExpectationReportDto> EvaluateAsync(
        ValidationAssertionDto assertion,
        CancellationToken cancellationToken)
    {
        if (assertion.Kind == ValidationExpectationDto.Url)
        {
            return new(
                nameof(ValidationExpectationDto.Url),
                assertion.Value,
                false,
                null,
                "Url expectations are invalid on a Desktop surface flow.");
        }

        string? actual = null;
        var error = await ValidationConditionWait.UntilAsync(
            async token =>
            {
                var snapshot = await OnUi(() => EvaluateNow(assertion), token);
                actual = snapshot.Actual;
                if (!string.IsNullOrWhiteSpace(snapshot.Error) && !snapshot.Passed)
                    actual = snapshot.Actual;
                return snapshot.Passed;
            },
            $"{assertion.Kind} '{assertion.Value}'",
            DelayAsync,
            cancellationToken);

        var last = await OnUi(() => EvaluateNow(assertion), cancellationToken);
        return new(
            assertion.Kind.ToString(),
            assertion.Value,
            error is null,
            last.Actual ?? actual,
            error is null ? null : last.Error ?? error);
    }

    public Task CapturePngAsync(string path, CancellationToken cancellationToken) =>
        OnUi(
            () =>
            {
                if (_window is null)
                    throw new InvalidOperationException("Desktop validation replay is not attached to a window.");
                capture.CapturePng(_window, path);
                return 0;
            },
            cancellationToken);

    private string? ExecuteOnUi(ValidationStepDto step)
    {
        if (step.Action == ValidationActionDto.Navigate)
            return Navigate(step.Value);

        var control = Resolve(step.Target);
        if (step.Action is ValidationActionDto.Click or ValidationActionDto.Fill && control is null)
            return MissingTarget(step.Target);

        switch (step.Action)
        {
            case ValidationActionDto.Click:
                input.Click(control!);
                return null;
            case ValidationActionDto.Fill:
                input.Fill(control!, step.Value ?? "");
                return null;
            case ValidationActionDto.Press:
                input.Press(control ?? _lastControl ?? _window, step.Value ?? "Enter");
                return null;
            default:
                return $"Unknown Desktop validation action '{step.Action}'.";
        }
    }

    private string? Navigate(string? route)
    {
        if (_viewModel is null)
            return "Desktop validation replay is not attached to a window.";
        return navigator.Navigate(_viewModel, route);
    }

    private DesktopExpectationSnapshot EvaluateNow(ValidationAssertionDto assertion)
    {
        return assertion.Kind switch
        {
            ValidationExpectationDto.Enabled => Enabled(assertion),
            ValidationExpectationDto.Visible => Visible(assertion),
            ValidationExpectationDto.Absent => Absent(assertion),
            ValidationExpectationDto.Text => Text(assertion),
            ValidationExpectationDto.Title => Title(assertion),
            ValidationExpectationDto.State => State(assertion, runningOnly: false),
            ValidationExpectationDto.Running => State(assertion, runningOnly: true),
            _ => new(false, null, $"Unknown Desktop expectation '{assertion.Kind}'.")
        };
    }

    private DesktopExpectationSnapshot Enabled(ValidationAssertionDto assertion)
    {
        var control = Resolve(assertion.Target);
        if (control is null)
            return new(false, null, MissingTarget(assertion.Target));
        var expected = !string.Equals(assertion.Value, "false", StringComparison.OrdinalIgnoreCase);
        return new(control.IsEffectivelyEnabled == expected, control.IsEffectivelyEnabled.ToString());
    }

    private DesktopExpectationSnapshot Visible(ValidationAssertionDto assertion)
    {
        var control = Resolve(assertion.Target);
        if (control is null)
            return new(false, null, MissingTarget(assertion.Target));
        var shown = control.IsEffectivelyVisible && control.IsAttachedToVisualTree();
        return new(shown, shown.ToString());
    }

    private DesktopExpectationSnapshot Absent(ValidationAssertionDto assertion)
    {
        var control = Resolve(assertion.Target);
        var present = control is not null && control.IsEffectivelyVisible && control.IsAttachedToVisualTree();
        return new(!present, present ? "present" : "absent");
    }

    private DesktopExpectationSnapshot Text(ValidationAssertionDto assertion)
    {
        var control = Resolve(assertion.Target);
        if (control is null)
            return new(false, null, MissingTarget(assertion.Target));
        var text = AccessibleText(control);
        var passed = text.Contains(assertion.Value, StringComparison.OrdinalIgnoreCase);
        return new(passed, text, passed ? null : $"Expected text '{assertion.Value}', but found '{text}'.");
    }

    private DesktopExpectationSnapshot Title(ValidationAssertionDto assertion)
    {
        var title = _window?.Title ?? "";
        var passed = string.Equals(title, assertion.Value, StringComparison.Ordinal)
                     || title.Contains(assertion.Value, StringComparison.OrdinalIgnoreCase);
        return new(passed, title, passed ? null : $"Expected title '{assertion.Value}', but found '{title}'.");
    }

    private DesktopExpectationSnapshot State(ValidationAssertionDto assertion, bool runningOnly)
    {
        if (_viewModel is null)
            return new(false, null, "Desktop validation replay is not attached to a window.");

        var expected = runningOnly ? "Running" : assertion.Value;
        var actual = ReadState(assertion.Target);
        if (actual is null)
            return new(false, null, "No workspace is selected to read a Desktop State expectation.");
        var passed = string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
        return new(passed, actual, passed ? null : $"Expected state '{expected}', but found '{actual}'.");
    }

    private string? ReadState(ValidationTargetDto? target)
    {
        var appName = target?.Name ?? target?.Text;
        if (!string.IsNullOrWhiteSpace(appName))
        {
            return _viewModel!.Applications.Applications
                .FirstOrDefault(application => string.Equals(application.Name, appName, StringComparison.OrdinalIgnoreCase))
                ?.State;
        }

        if (!string.IsNullOrWhiteSpace(target?.TestId))
        {
            var workspace = _viewModel!.Sidebar.Workspaces
                .FirstOrDefault(item => string.Equals(item.Id, target.TestId, StringComparison.Ordinal));
            if (workspace is not null)
                return workspace.State;
        }

        return _viewModel!.Sidebar.SelectedWorkspace?.State;
    }

    private Control? Resolve(ValidationTargetDto? target)
    {
        LastResolution = null;
        LastCandidates = [];
        if (_window is null || target is null)
            return null;

        var control = locator.Resolve(_window, target, out var candidates);
        LastCandidates = candidates.Count == 0 ? locator.Nearby(_window, target) : candidates;
        if (control is not null)
        {
            LastResolution = locator.Describe(control);
            _lastControl = control;
        }

        return control;
    }

    private string MissingTarget(ValidationTargetDto? target)
    {
        var wanted = target?.TestId ?? target?.Name ?? target?.Label ?? target?.Text ?? target?.Role ?? "(none)";
        var candidates = LastCandidates.Count == 0
            ? "none"
            : string.Join(", ", LastCandidates.Select(candidate => candidate.AutomationId ?? candidate.Name ?? candidate.ControlType));
        return $"Target '{wanted}' was not found. Candidates: {candidates}.";
    }

    private static string AccessibleText(Control control)
    {
        if (control is ContentControl { Content: string content })
            return content;
        if (control is TextBox textBox)
            return textBox.Text ?? "";
        if (control is TextBlock textBlock)
            return textBlock.Text ?? "";
        return AutomationProperties.GetName(control) ?? control.Name ?? "";
    }

    private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, cancellationToken);

    private static async Task FlushAsync() =>
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);

    private async Task<T> OnUi<T>(Func<T> work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Dispatcher.UIThread.CheckAccess())
            return work();

        return await Dispatcher.UIThread.InvokeAsync(work);
    }
}

using System.Diagnostics;
using System.Text.Json;
using AgentUp.Desktop.Features.Browser.Controllers;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Interfaces;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Validation.ViewModels;

namespace AgentUp.Desktop.Features.Validation.Services;

public sealed class ValidationFlowReplayService(
    ValidationFlowApiClient client,
    BrowserInteractionController browser,
    IDesktopValidationReplayHost? desktopHost = null,
    IValidationArtifactStore? artifacts = null,
    bool skipCosmeticStaging = false) : IValidationReplayConnector
{
    private IValidationReplayHost? _host;

    public void Connect(IValidationReplayHost host) => _host = host;

    public Task<ValidationReplayResult> RunAsync(
        string workspaceId,
        string flowId,
        IValidationReplayReporter reporter,
        CancellationToken cancellationToken = default)
        => RunAsync(workspaceId, flowId, reporter, _host, cancellationToken);

    internal async Task<ValidationReplayResult> RunAsync(
        string workspaceId,
        string flowId,
        IValidationReplayReporter reporter,
        IValidationReplayHost? host,
        CancellationToken cancellationToken)
    {
        var flow = await client.GetAsync(workspaceId, flowId, cancellationToken);
        if (flow is null)
        {
            reporter.CompleteFlow(false, "Validation flow was not found.");
            return new(false, "Validation flow was not found.");
        }

        if (flow.Surface == ValidationSurfaceDto.Desktop)
            return await RunDesktopAsync(flow, reporter, artifactRoot: null, cancellationToken);

        if (host is null)
        {
            reporter.CompleteFlow(false, "Validation replay is not connected to the browser view.");
            return new(false, "Validation replay is not connected to the browser view.");
        }

        reporter.BeginFlow();

        var origin = host.ResolveApplicationOrigin(workspaceId, flow.Application);
        if (origin is null)
        {
            reporter.CompleteFlow(false, "The flow application has no allocated HTTP port.");
            return new(false, "The flow application has no allocated HTTP port.");
        }

        var startUrl = $"{new Uri(new Uri(origin), flow.InitialPath)}";
        if (!await host.PrepareViewportAsync(workspaceId, flow.Application, startUrl, cancellationToken))
        {
            reporter.CompleteFlow(false, "Could not open the application browser tab for this flow.");
            return new(false, "Could not open the application browser tab for this flow.");
        }

        await WaitForPageReadyAsync(host, workspaceId, cancellationToken);

        reporter.BeginStage(ValidationFlowItemViewModel.InitialStageId);
        var initialError = await AssertAsync(
            host,
            workspaceId,
            ValidationFlowItemViewModel.InitialStageId,
            flow.InitialExpectations,
            reporter,
            cancellationToken);
        if (initialError is not null)
        {
            reporter.CompleteStage(ValidationFlowItemViewModel.InitialStageId, false, initialError);
            reporter.CompleteFlow(false, initialError);
            return new(false, initialError);
        }

        reporter.CompleteStage(ValidationFlowItemViewModel.InitialStageId, true);

        foreach (var step in flow.Steps)
        {
            reporter.BeginStage(step.Id);
            var stepError = await ExecuteStepAsync(host, workspaceId, origin, step, cancellationToken);
            if (stepError is not null)
            {
                reporter.CompleteStage(step.Id, false, stepError);
                reporter.CompleteFlow(false, stepError);
                return new(false, stepError, step.Id);
            }

            var assertionError = await AssertAsync(
                host,
                workspaceId,
                step.Id,
                step.Expectations ?? [],
                reporter,
                cancellationToken);
            if (assertionError is not null)
            {
                reporter.CompleteStage(step.Id, false, assertionError);
                reporter.CompleteFlow(false, assertionError);
                return new(false, assertionError, step.Id);
            }

            reporter.CompleteStage(step.Id, true);
        }

        var message = $"Validation '{flow.Name}' passed.";
        reporter.CompleteFlow(true, message);
        return new(true, message);
    }

    private async Task<string?> ExecuteStepAsync(
        IValidationReplayHost host,
        string workspaceId,
        string origin,
        ValidationStepDto step,
        CancellationToken cancellationToken)
        => step.Action switch
        {
            ValidationActionDto.Navigate => await NavigateStepAsync(
                host,
                workspaceId,
                $"{new Uri(new Uri(origin), step.Value ?? "/")}",
                cancellationToken),
            ValidationActionDto.Click when step.Target?.Selector is { } selector
                => await StagedClickAsync(host, workspaceId, selector, cancellationToken),
            ValidationActionDto.Fill when step.Target?.Selector is { } selector
                => await StagedFillAsync(host, workspaceId, selector, step.Value ?? "", cancellationToken),
            ValidationActionDto.Press
                => await ExecuteScriptAsync(host, workspaceId, browser.PressScript(step.Value ?? "Enter"), cancellationToken),
            _ => "Watched replay requires a selector fallback for click and fill steps."
        };

    private async Task<string?> NavigateStepAsync(
        IValidationReplayHost host,
        string workspaceId,
        string url,
        CancellationToken cancellationToken)
    {
        await host.NavigateAsync(workspaceId, url, cancellationToken);
        await WaitForPageReadyAsync(host, workspaceId, cancellationToken);
        return null;
    }

    private async Task<string?> StagedClickAsync(
        IValidationReplayHost host,
        string workspaceId,
        string selector,
        CancellationToken cancellationToken)
    {
        if (!skipCosmeticStaging)
        {
            var moveError = await ExecuteScriptAsync(host, workspaceId, browser.BeginMouseMoveScript(selector), cancellationToken);
            if (moveError is not null) return moveError;
            await host.DelayAsync(TimeSpan.FromMilliseconds(browser.AnimationMs), cancellationToken);

            var pingError = await ExecuteScriptAsync(host, workspaceId, browser.BeginAttentionPingScript(selector), cancellationToken);
            if (pingError is not null) return pingError;
            await host.DelayAsync(TimeSpan.FromMilliseconds(browser.AnimationMs), cancellationToken);
        }

        var clickError = await ExecuteScriptAsync(host, workspaceId, browser.CompleteClickScript(selector), cancellationToken);
        if (clickError is not null) return clickError;

        if (!skipCosmeticStaging)
            await host.DelayAsync(TimeSpan.FromMilliseconds(browser.AnimationMs), cancellationToken);
        await WaitForPageReadyAsync(host, workspaceId, cancellationToken);
        return null;
    }

    private async Task<string?> StagedFillAsync(
        IValidationReplayHost host,
        string workspaceId,
        string selector,
        string text,
        CancellationToken cancellationToken)
    {
        if (!skipCosmeticStaging)
        {
            var moveError = await ExecuteScriptAsync(host, workspaceId, browser.BeginMouseMoveScript(selector), cancellationToken);
            if (moveError is not null) return moveError;
            await host.DelayAsync(TimeSpan.FromMilliseconds(browser.AnimationMs), cancellationToken);

            var pingError = await ExecuteScriptAsync(host, workspaceId, browser.BeginAttentionPingScript(selector), cancellationToken);
            if (pingError is not null) return pingError;
            await host.DelayAsync(TimeSpan.FromMilliseconds(browser.AnimationMs), cancellationToken);
        }

        var fillError = await ExecuteScriptAsync(host, workspaceId, browser.FillScript(selector, text), cancellationToken);
        if (!skipCosmeticStaging)
            await ExecuteScriptAsync(host, workspaceId, browser.RemoveAttentionPingScript(), cancellationToken);
        return fillError;
    }

    private async Task<string?> AssertAsync(
        IValidationReplayHost host,
        string workspaceId,
        string stageId,
        IReadOnlyList<ValidationAssertionDto> assertions,
        IValidationReplayReporter reporter,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < assertions.Count; index++)
        {
            reporter.BeginCheck(stageId, index);
            var assertion = assertions[index];
            var error = assertion.Kind switch
            {
                ValidationExpectationDto.Text => await WaitForConditionAsync(
                    host,
                    workspaceId,
                    browser.CheckTextScript(assertion.Value),
                    cancellationToken),
                ValidationExpectationDto.Visible when assertion.Target?.Selector is { } selector
                    => await WaitForConditionAsync(
                        host,
                        workspaceId,
                        browser.CheckSelectorScript(selector),
                        cancellationToken),
                ValidationExpectationDto.Url => await WaitForUrlAsync(host, workspaceId, assertion.Value, cancellationToken),
                ValidationExpectationDto.Title => await WaitForTitleAsync(host, workspaceId, assertion.Value, cancellationToken),
                _ => "Watched replay requires a selector fallback for visibility checks."
            };

            reporter.CompleteCheck(stageId, index, error is null, error);
            if (error is not null)
                return error;
        }

        return null;
    }

    private async Task<string?> WaitForUrlAsync(
        IValidationReplayHost host,
        string workspaceId,
        string expected,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var actual = await host.EvalAsync(workspaceId, browser.GetUrlScript(), cancellationToken);
            if (actual is not null)
            {
                if (expected.StartsWith('/')
                    && Uri.TryCreate(actual.Trim('"'), UriKind.Absolute, out var uri)
                    && string.Equals(uri.PathAndQuery, expected, StringComparison.Ordinal))
                    return null;
                if (string.Equals(actual.Trim('"'), expected, StringComparison.Ordinal))
                    return null;
            }

            await host.DelayAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        return $"Expected url '{expected}', but the page did not reach it in time.";
    }

    private async Task<string?> WaitForTitleAsync(
        IValidationReplayHost host,
        string workspaceId,
        string expected,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var actual = await host.EvalAsync(workspaceId, browser.GetTitleScript(), cancellationToken);
            if (string.Equals(actual?.Trim('"'), expected, StringComparison.Ordinal))
                return null;

            await host.DelayAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        return $"Expected title '{expected}', but the page did not reach it in time.";
    }

    private async Task<string?> WaitForConditionAsync(
        IValidationReplayHost host,
        string workspaceId,
        string predicateScript,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await host.EvalAsync(workspaceId, predicateScript, cancellationToken);
            if (string.Equals(result?.Trim(), "true", StringComparison.OrdinalIgnoreCase))
                return null;

            await host.DelayAsync(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        return "Expected content did not appear in time.";
    }

    private async Task WaitForPageReadyAsync(
        IValidationReplayHost host,
        string workspaceId,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = await host.EvalAsync(workspaceId, browser.CheckNavigationScript(), cancellationToken);
            if (string.Equals(state?.Trim('"'), "complete", StringComparison.OrdinalIgnoreCase)
                || string.Equals(state?.Trim('"'), "interactive", StringComparison.OrdinalIgnoreCase))
                return;

            await host.DelayAsync(TimeSpan.FromMilliseconds(125), cancellationToken);
        }
    }

    private async Task<string?> ExecuteScriptAsync(
        IValidationReplayHost host,
        string workspaceId,
        string script,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var json = await host.EvalAsync(workspaceId, script, cancellationToken);
        return ParseScriptError(json);
    }

    private static string? ParseScriptError(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return "Browser script returned no result.";

        if (!json.TrimStart().StartsWith('{'))
            return null;

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("error", out var error))
                return error.GetString() ?? "Browser script failed.";
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    internal Task<ValidationReplayResult> RunFlowAsync(
        ValidationFlowDto flow,
        IValidationReplayReporter reporter,
        string? artifactRoot,
        CancellationToken cancellationToken)
        => flow.Surface == ValidationSurfaceDto.Desktop
            ? RunDesktopAsync(flow, reporter, artifactRoot, cancellationToken)
            : RunAsync(flow.WorkspaceId, flow.Id, reporter, _host, cancellationToken);

    private async Task<ValidationReplayResult> RunDesktopAsync(
        ValidationFlowDto flow,
        IValidationReplayReporter reporter,
        string? artifactRoot,
        CancellationToken cancellationToken)
    {
        if (desktopHost is null)
        {
            reporter.CompleteFlow(false, "Desktop validation replay is not attached to a window.");
            return new(false, "Desktop validation replay is not attached to a window.");
        }

        reporter.BeginFlow();
        reporter.BeginStage(ValidationFlowItemViewModel.InitialStageId);
        var started = DateTimeOffset.UtcNow;
        var timer = Stopwatch.StartNew();
        var steps = new List<ValidationStepReportDto>();
        var directory = artifactRoot is null || artifacts is null
            ? null
            : artifacts.FlowDirectory(artifactRoot, flow.Id);

        var prepareError = await desktopHost.PrepareAsync(flow, cancellationToken);
        var initialShot = await CaptureStepAsync(directory, "00-initial.png", cancellationToken);
        var initialExpectations = await AssertDesktopAsync(
            flow.InitialExpectations,
            ValidationFlowItemViewModel.InitialStageId,
            reporter,
            cancellationToken);
        steps.Add(InitialReport(flow, initialShot, initialExpectations, prepareError));
        if (prepareError is not null || initialExpectations.Error is not null)
        {
            var message = prepareError ?? initialExpectations.Error!;
            reporter.CompleteStage(ValidationFlowItemViewModel.InitialStageId, false, message);
            reporter.CompleteFlow(false, message);
            await WriteReportAsync(directory, flow, false, message, started, timer.ElapsedMilliseconds, steps, cancellationToken);
            return new(false, message);
        }

        reporter.CompleteStage(ValidationFlowItemViewModel.InitialStageId, true);

        var index = 1;
        foreach (var step in flow.Steps)
        {
            reporter.BeginStage(step.Id);
            var stepStarted = Stopwatch.StartNew();
            var stepError = await desktopHost.ExecuteAsync(step, cancellationToken);
            var resolution = desktopHost.LastResolution;
            var candidates = desktopHost.LastCandidates;
            var expectations = await AssertDesktopAsync(
                step.Expectations ?? [],
                step.Id,
                reporter,
                cancellationToken);
            var shot = await CaptureStepAsync(directory, $"{index:00}-{SafeFile(step.Id)}.png", cancellationToken);
            steps.Add(new ValidationStepReportDto(
                step.Id,
                step.Description,
                step.Action.ToString(),
                step.Target,
                step.Value,
                resolution,
                candidates,
                expectations.Reports,
                stepError is null && expectations.Error is null,
                stepStarted.ElapsedMilliseconds,
                shot));
            var failure = stepError ?? expectations.Error;
            if (failure is not null)
            {
                reporter.CompleteStage(step.Id, false, failure);
                reporter.CompleteFlow(false, failure);
                await WriteReportAsync(directory, flow, false, failure, started, timer.ElapsedMilliseconds, steps, cancellationToken);
                return new(false, failure, step.Id);
            }

            reporter.CompleteStage(step.Id, true);
            index++;
        }

        var passed = $"Validation '{flow.Name}' passed.";
        reporter.CompleteFlow(true, passed);
        await WriteReportAsync(directory, flow, true, passed, started, timer.ElapsedMilliseconds, steps, cancellationToken);
        return new(true, passed);
    }

    private async Task<(IReadOnlyList<ValidationExpectationReportDto> Reports, string? Error)> AssertDesktopAsync(
        IReadOnlyList<ValidationAssertionDto> assertions,
        string stageId,
        IValidationReplayReporter reporter,
        CancellationToken cancellationToken)
    {
        var reports = new List<ValidationExpectationReportDto>();
        for (var index = 0; index < assertions.Count; index++)
        {
            reporter.BeginCheck(stageId, index);
            var report = await desktopHost!.EvaluateAsync(assertions[index], cancellationToken);
            reports.Add(report);
            reporter.CompleteCheck(stageId, index, report.Passed, report.Error);
            if (!report.Passed)
                return (reports, report.Error ?? $"Expected {report.Kind} '{report.Value}'.");
        }

        return (reports, null);
    }

    private async Task<string> CaptureStepAsync(
        string? directory,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (directory is null || artifacts is null || desktopHost is null)
            return fileName;

        var path = Path.Join(directory, fileName);
        await artifacts.WriteScreenshotAsync(path, desktopHost.CapturePngAsync, cancellationToken);
        return fileName;
    }

    private async Task WriteReportAsync(
        string? directory,
        ValidationFlowDto flow,
        bool passed,
        string message,
        DateTimeOffset started,
        long durationMs,
        IReadOnlyList<ValidationStepReportDto> steps,
        CancellationToken cancellationToken)
    {
        if (directory is null || artifacts is null)
            return;

        await artifacts.WriteReportAsync(
            directory,
            new ValidationRunReportDto(
                flow.Id,
                flow.Name,
                flow.Surface.ToString(),
                passed,
                message,
                started,
                durationMs,
                steps),
            cancellationToken);
    }

    private static ValidationStepReportDto InitialReport(
        ValidationFlowDto flow,
        string screenshot,
        (IReadOnlyList<ValidationExpectationReportDto> Reports, string? Error) expectations,
        string? prepareError)
    {
        return new(
            ValidationFlowItemViewModel.InitialStageId,
            "Initial surface",
            ValidationActionDto.Navigate.ToString(),
            null,
            flow.InitialPath,
            null,
            [],
            expectations.Reports,
            prepareError is null && expectations.Error is null,
            0,
            screenshot);
    }

    private static string SafeFile(string stepId)
    {
        var chars = stepId.Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_').ToArray();
        var value = new string(chars);
        return string.IsNullOrWhiteSpace(value) ? "step" : value;
    }
}

public sealed record ValidationReplayResult(bool Succeeded, string Message, string? StepId = null);

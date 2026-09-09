using System.Text.Json;
using AgentUp.Desktop.Features.Browser.Controllers;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Interfaces;
using AgentUp.Desktop.Features.Validation.Providers;
using AgentUp.Desktop.Features.Validation.ViewModels;

namespace AgentUp.Desktop.Features.Validation.Services;

public sealed class ValidationFlowReplayService(
    ValidationFlowApiClient client,
    BrowserInteractionController browser) : IValidationReplayConnector
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
        if (host is null)
        {
            reporter.CompleteFlow(false, "Validation replay is not connected to the browser view.");
            return new(false, "Validation replay is not connected to the browser view.");
        }

        reporter.BeginFlow();

        var flow = await client.GetAsync(workspaceId, flowId, cancellationToken);
        if (flow is null)
        {
            reporter.CompleteFlow(false, "Validation flow was not found.");
            return new(false, "Validation flow was not found.");
        }

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
        var moveError = await ExecuteScriptAsync(host, workspaceId, browser.BeginMouseMoveScript(selector), cancellationToken);
        if (moveError is not null) return moveError;
        await host.DelayAsync(TimeSpan.FromMilliseconds(browser.AnimationMs), cancellationToken);

        var pingError = await ExecuteScriptAsync(host, workspaceId, browser.BeginAttentionPingScript(selector), cancellationToken);
        if (pingError is not null) return pingError;
        await host.DelayAsync(TimeSpan.FromMilliseconds(browser.AnimationMs), cancellationToken);

        var clickError = await ExecuteScriptAsync(host, workspaceId, browser.CompleteClickScript(selector), cancellationToken);
        if (clickError is not null) return clickError;

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
        var moveError = await ExecuteScriptAsync(host, workspaceId, browser.BeginMouseMoveScript(selector), cancellationToken);
        if (moveError is not null) return moveError;
        await host.DelayAsync(TimeSpan.FromMilliseconds(browser.AnimationMs), cancellationToken);

        var pingError = await ExecuteScriptAsync(host, workspaceId, browser.BeginAttentionPingScript(selector), cancellationToken);
        if (pingError is not null) return pingError;
        await host.DelayAsync(TimeSpan.FromMilliseconds(browser.AnimationMs), cancellationToken);

        var fillError = await ExecuteScriptAsync(host, workspaceId, browser.FillScript(selector, text), cancellationToken);
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
}

public sealed record ValidationReplayResult(bool Succeeded, string Message, string? StepId = null);

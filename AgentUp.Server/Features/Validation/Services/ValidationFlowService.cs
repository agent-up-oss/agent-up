using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Shared.Interfaces;
using System.Text.Json;
using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Interfaces;
using AgentUp.Server.Features.Validation.Providers;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.DesktopApplications.Controllers;
namespace AgentUp.Server.Features.Validation.Services;

public sealed class ValidationFlowService(IValidationFlowRepository repository, WorkspaceQueryController workspaces, BrowserMcpTools browser, PlaywrightFlowExporter exporter, DesktopMcpTools? desktop = null)
{
    internal const string InitialStageId = "__initial__";

    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task<IReadOnlyList<ValidationFlow>> ListAsync(string workspaceId, string application, CancellationToken ct = default) =>
        (await repository.LoadAsync(workspaceId, ct)).Where(x => x.Application == application).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public async Task<ValidationFlow?> GetAsync(string workspaceId, string id, CancellationToken ct = default) =>
        (await repository.LoadAsync(workspaceId, ct)).SingleOrDefault(x => x.Id == id);

    public async Task<SaveValidationFlowResult> SaveAsync(string workspaceId, SaveValidationFlowRequest request, CancellationToken ct = default)
    {
        var error = Validate(workspaceId, request);
        if (error is not null)
            return new SaveValidationFlowResult(false, Error: error);

        await _gate.WaitAsync(ct);
        try
        {
            var flows = (await repository.LoadAsync(workspaceId, ct)).ToList();
            var id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id;
            var existing = flows.FindIndex(x => x.Id == id);
            var flow = new ValidationFlow(id!, workspaceId, request.Application.Trim(), request.Name.Trim(), request.Description.Trim(), request.InitialPath.Trim(), request.InitialExpectations ?? [], request.Steps, DateTimeOffset.UtcNow, existing < 0 ? 1 : flows[existing].Version + 1);
            if (existing < 0) flows.Add(flow); else flows[existing] = flow;
            await repository.SaveAsync(workspaceId, flows, ct);
            return new SaveValidationFlowResult(true, flow);
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> DeleteAsync(string workspaceId, string id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var flows = (await repository.LoadAsync(workspaceId, ct)).ToList();
            var removed = flows.RemoveAll(x => x.Id == id) > 0;
            if (removed)
                await repository.SaveAsync(workspaceId, flows, ct);
            return removed;
        }
        finally { _gate.Release(); }
    }

    public async Task<PlaywrightExport?> ExportAsync(string workspaceId, string id, CancellationToken ct = default)
    {
        var flow = (await repository.LoadAsync(workspaceId, ct)).SingleOrDefault(x => x.Id == id);
        return flow is null ? null : exporter.Export(flow);
    }

    public async Task<ValidationRunResult> RunAsync(string workspaceId, string id, CancellationToken ct = default)
    {
        var flow = (await repository.LoadAsync(workspaceId, ct)).SingleOrDefault(x => x.Id == id);
        if (flow is null) return new(false, "Validation flow was not found.");
        var workspace = workspaces.GetById(workspaceId);
        var app = workspace?.Applications.SingleOrDefault(x => x.Name == flow.Application);
        if (app?.Kind == ApplicationKind.Desktop)
            return await RunDesktopAsync(workspaceId, flow, ct);
        var port = app?.AllocatedPorts.FirstOrDefault(x => x.Protocol.Equals("http", StringComparison.OrdinalIgnoreCase));
        if (port is null) return new(false, "The flow application has no allocated HTTP port.");
        var origin = $"http://127.0.0.1:{port.AllocatedPort}";
        var start = await browser.Navigate(workspaceId, new Uri(new Uri(origin), flow.InitialPath).ToString(), ct);
        if (!start.Succeeded) return new(false, start.Message);
        var initial = await AssertAsync(workspaceId, flow.InitialExpectations, ct);
        if (initial is not null) return new(false, initial);
        foreach (var step in flow.Steps)
        {
            var result = step.Action switch
            {
                ValidationAction.Navigate => await browser.Navigate(workspaceId, new Uri(new Uri(origin), step.Value ?? "/").ToString(), ct),
                ValidationAction.Click when step.Target?.Selector is { } selector => await browser.Click(workspaceId, selector, ct),
                ValidationAction.Fill when step.Target?.Selector is { } selector => await browser.Fill(workspaceId, selector, step.Value ?? "", ct),
                ValidationAction.Press => await browser.Press(workspaceId, step.Value ?? "Enter", ct),
                _ => new McpToolResult(false, "Watched replay currently requires a selector fallback for click and fill steps.")
            };
            if (!result.Succeeded) return new(false, result.Message, step.Id);
            if (step.Action is ValidationAction.Click or ValidationAction.Navigate)
            {
                var navigation = await browser.WaitForNavigation(workspaceId, 10_000, ct);
                if (!navigation.Succeeded) return new(false, navigation.Message, step.Id);
            }
            var error = await AssertAsync(workspaceId, step.Expectations ?? [], ct);
            if (error is not null) return new(false, error, step.Id);
        }
        return new(true, $"Validation '{flow.Name}' passed.");
    }

    private async Task<ValidationRunResult> RunDesktopAsync(string workspaceId, ValidationFlow flow, CancellationToken ct)
    {
        if (desktop is null) return new(false, "Desktop validation is unavailable.");
        var inspected = await desktop.Inspect(workspaceId, flow.Application);
        if (!inspected.Succeeded) return new(false, inspected.Message);
        var generation = ReadGeneration(inspected.Data);
        if (generation is null) return new(false, "Desktop inspection returned no session generation.");
        var initialError = await AssertDesktopAsync(workspaceId, flow.Application, flow.InitialExpectations, ct);
        if (initialError is not null) return new(false, initialError);
        foreach (var step in flow.Steps)
        {
            var result = await RunDesktopStepAsync(workspaceId, flow.Application, generation.Value, step, ct);
            if (!result.Succeeded) return new(false, result.Message, step.Id);
            var error = await AssertDesktopAsync(workspaceId, flow.Application, step.Expectations ?? [], ct);
            if (error is not null) return new(false, error, step.Id);
        }
        return new(true, $"Validation '{flow.Name}' passed.");
    }

    private async Task<string?> AssertDesktopAsync(string workspaceId, string application, IReadOnlyList<ValidationAssertion> assertions, CancellationToken ct)
    {
        var inspected = await desktop!.Inspect(workspaceId, application);
        if (!inspected.Succeeded) return inspected.Message;
        if (assertions.Any(assertion => assertion.Kind is not (ValidationExpectation.Running or ValidationExpectation.Visible)))
            return "Desktop validation currently supports Running and framebuffer Visible expectations.";
        if (assertions.Any(assertion => assertion.Kind == ValidationExpectation.Visible))
        {
            var screenshot = await desktop.Screenshot(workspaceId, application, ct);
            if (screenshot.IsError == true) return "Desktop framebuffer was not visible.";
        }
        return null;
    }

    private async Task<McpToolResult> RunDesktopStepAsync(string workspaceId, string application, long generation, ValidationStep step, CancellationToken ct)
    {
        if (desktop is null) return new(false, "Desktop validation is unavailable.");
        return step.Action switch
        {
            ValidationAction.Click when step.Target is { X: { } x, Y: { } y } =>
                await desktop.Click(workspaceId, application, generation, x, y, 0, ct),
            ValidationAction.Fill when step.Target is { X: { } x, Y: { } y } =>
                await desktop.Fill(workspaceId, application, generation, x, y, step.Value ?? string.Empty, ct),
            ValidationAction.Press => await desktop.Press(workspaceId, application, generation, step.Value ?? "Enter", ct),
            _ => new McpToolResult(false, "Desktop validation supports coordinate click/fill and key press steps.")
        };
    }

    private static long? ReadGeneration(object? data)
    {
        if (data is null) return null;
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(data));
        return document.RootElement.TryGetProperty("generation", out var value) ? value.GetInt64() : null;
    }

    private async Task<string?> AssertAsync(string workspaceId, IReadOnlyList<ValidationAssertion> assertions, CancellationToken ct)
    {
        foreach (var assertion in assertions)
        {
            var result = assertion.Kind switch
            {
                ValidationExpectation.Text => await browser.WaitForText(workspaceId, assertion.Value, 10_000, ct),
                ValidationExpectation.Visible when assertion.Target?.Selector is { } selector => await browser.WaitForSelector(workspaceId, selector, 10_000, ct),
                ValidationExpectation.Url => await InspectEqualsAsync(workspaceId, "url", assertion.Value, ct),
                ValidationExpectation.Title => await InspectEqualsAsync(workspaceId, "title", assertion.Value, ct),
                _ => new McpToolResult(false, "Watched replay requires a selector fallback for visibility checks.")
            };
            if (!result.Succeeded) return result.Message;
        }
        return null;
    }

    private async Task<McpToolResult> InspectEqualsAsync(string workspaceId, string property, string expected, CancellationToken ct)
    {
        var inspected = await browser.InspectPage(workspaceId, ct);
        if (!inspected.Succeeded || inspected.Data is not string json)
            return new McpToolResult(false, inspected.Message);
        try
        {
            using var document = JsonDocument.Parse(json);
            var actual = document.RootElement.TryGetProperty(property, out var value) ? value.GetString() : null;
            var matches = property == "url" && expected.StartsWith('/')
                ? Uri.TryCreate(actual, UriKind.Absolute, out var uri) && string.Equals(uri.PathAndQuery, expected, StringComparison.Ordinal)
                : string.Equals(actual, expected, StringComparison.Ordinal);
            return matches ? new McpToolResult(true, $"{property} matched.") : new McpToolResult(false, $"Expected {property} '{expected}', but found '{actual}'.");
        }
        catch (JsonException)
        {
            return new McpToolResult(false, "Browser inspection returned invalid page state.");
        }
    }

    private string? Validate(string workspaceId, SaveValidationFlowRequest request)
    {
        if (workspaces.GetById(workspaceId) is null)
            return "Workspace was not found.";
        if (!workspaces.HasApplication(workspaceId, request.Application))
            return "Application was not found in this workspace.";
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120)
            return "Name is required and cannot exceed 120 characters.";
        var desktopApplication = workspaces.GetById(workspaceId)?.Applications
            .FirstOrDefault(application => application.Name == request.Application)?.Kind == ApplicationKind.Desktop;
        if (!desktopApplication && !IsSafeRelativePath(request.InitialPath))
            return "InitialPath must be a local application-relative path beginning with one '/'.";
        if (request.Description is null)
            return "Description is required.";
        if (request.Steps is null || request.Steps.Count is < 1 or > 200)
            return "A flow requires 1 to 200 steps.";
        if (request.InitialExpectations is null || request.InitialExpectations.Count == 0)
            return "The initial place requires at least one visible expectation.";
        if (request.Steps.Any(step => string.IsNullOrWhiteSpace(step.Description)))
            return "Every step requires a GUI-level description.";
        if (request.Steps.Any(step => step.Expectations is null || step.Expectations.Count == 0))
            return "Every step requires at least one visible expectation.";
        if (request.Steps.Any(step => step.Action == ValidationAction.Navigate && (desktopApplication || !IsSafeRelativePath(step.Value))))
            return "Navigate steps require a local application-relative path beginning with one '/'.";
        if (request.Steps.Any(step => step.Action is ValidationAction.Click or ValidationAction.Fill
                                      && (desktopApplication
                                          ? step.Target?.X is null || step.Target.Y is null
                                          : string.IsNullOrWhiteSpace(step.Target?.Selector))))
            return desktopApplication
                ? "Desktop click and fill steps require logical framebuffer X and Y coordinates."
                : "Click and fill steps require a stable selector fallback for watched replay.";
        return null;
    }

    private static bool IsSafeRelativePath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.StartsWith("/", StringComparison.Ordinal)
        && !path.StartsWith("//", StringComparison.Ordinal)
        && !path.Any(char.IsControl)
        && Uri.TryCreate(path, UriKind.Relative, out _);

}

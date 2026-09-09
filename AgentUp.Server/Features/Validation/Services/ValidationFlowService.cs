using AgentUp.Server.Features.Browser.Controllers;
using AgentUp.Server.Shared.Interfaces;
using System.Text.Json;
using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Interfaces;
using AgentUp.Server.Features.Validation.Providers;
using AgentUp.Server.Features.Workspaces.Controllers;
namespace AgentUp.Server.Features.Validation.Services;

public sealed class ValidationFlowService(IValidationFlowRepository repository, WorkspaceQueryController workspaces, BrowserMcpTools browser, PlaywrightFlowExporter exporter)
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
        if (!IsSafeRelativePath(request.InitialPath))
            return "InitialPath must be a local application-relative path beginning with one '/'.";
        if (request.Steps.Count is < 1 or > 200)
            return "A flow requires 1 to 200 steps.";
        if (request.InitialExpectations is null || request.InitialExpectations.Count == 0)
            return "The initial place requires at least one visible expectation.";
        if (request.Steps.Any(step => string.IsNullOrWhiteSpace(step.Description)))
            return "Every step requires a GUI-level description.";
        if (request.Steps.Any(step => step.Expectations is null || step.Expectations.Count == 0))
            return "Every step requires at least one visible expectation.";
        if (request.Steps.Any(step => step.Action == ValidationAction.Navigate && !IsSafeRelativePath(step.Value)))
            return "Navigate steps require a local application-relative path beginning with one '/'.";
        if (request.Steps.Any(step => step.Action is ValidationAction.Click or ValidationAction.Fill && string.IsNullOrWhiteSpace(step.Target?.Selector)))
            return "Click and fill steps require a stable selector fallback for watched replay.";
        return null;
    }

    private static bool IsSafeRelativePath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.StartsWith("/", StringComparison.Ordinal)
        && !path.StartsWith("//", StringComparison.Ordinal)
        && !path.Any(char.IsControl)
        && Uri.TryCreate(path, UriKind.Relative, out _);

}

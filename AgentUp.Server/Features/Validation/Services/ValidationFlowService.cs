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
    private readonly SemaphoreSlim _gate = new(1, 1);
    public async Task<IReadOnlyList<ValidationFlow>> ListAsync(string workspaceId, string application, CancellationToken ct = default) =>
        (await repository.LoadAsync(ct)).Where(x => x.WorkspaceId == workspaceId && x.Application == application).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();

    public async Task<ValidationFlow> SaveAsync(string workspaceId, SaveValidationFlowRequest request, CancellationToken ct = default)
    {
        Validate(workspaceId, request);
        await _gate.WaitAsync(ct);
        try
        {
            var flows = (await repository.LoadAsync(ct)).ToList();
            var id = string.IsNullOrWhiteSpace(request.Id) ? Guid.NewGuid().ToString("N") : request.Id;
            var existing = flows.FindIndex(x => x.Id == id && x.WorkspaceId == workspaceId);
            var flow = new ValidationFlow(id!, workspaceId, request.Application.Trim(), request.Name.Trim(), request.Description.Trim(), request.InitialPath.Trim(), request.InitialExpectations ?? [], request.Steps, DateTimeOffset.UtcNow, existing < 0 ? 1 : flows[existing].Version + 1);
            if (existing < 0) flows.Add(flow); else flows[existing] = flow;
            await repository.SaveAsync(flows, ct);
            return flow;
        }
        finally { _gate.Release(); }
    }

    public async Task<bool> DeleteAsync(string workspaceId, string id, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try { var flows = (await repository.LoadAsync(ct)).ToList(); var removed = flows.RemoveAll(x => x.WorkspaceId == workspaceId && x.Id == id) > 0; if (removed) await repository.SaveAsync(flows, ct); return removed; }
        finally { _gate.Release(); }
    }

    public async Task<PlaywrightExport?> ExportAsync(string workspaceId, string id, CancellationToken ct = default)
    { var flow = (await repository.LoadAsync(ct)).SingleOrDefault(x => x.WorkspaceId == workspaceId && x.Id == id); return flow is null ? null : exporter.Export(flow); }

    public async Task<ValidationRunResult> RunAsync(string workspaceId, string id, CancellationToken ct = default)
    {
        var flow = (await repository.LoadAsync(ct)).SingleOrDefault(x => x.WorkspaceId == workspaceId && x.Id == id);
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

    private void Validate(string workspaceId, SaveValidationFlowRequest request)
    {
        if (workspaces.GetById(workspaceId) is null) throw new ArgumentException("Workspace was not found.");
        if (!workspaces.HasApplication(workspaceId, request.Application)) throw new ArgumentException("Application was not found in this workspace.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120) throw new ArgumentException("Name is required and cannot exceed 120 characters.");
        if (!Uri.TryCreate(request.InitialPath, UriKind.Relative, out _) || !request.InitialPath.StartsWith('/')) throw new ArgumentException("InitialPath must be an application-relative path beginning with '/'.");
        if (request.Steps.Count is < 1 or > 200) throw new ArgumentException("A flow requires 1 to 200 steps.");
        if (request.InitialExpectations is null || request.InitialExpectations.Count == 0) throw new ArgumentException("The initial place requires at least one visible expectation.");
        if (request.Steps.Any(x => string.IsNullOrWhiteSpace(x.Description))) throw new ArgumentException("Every step requires a GUI-level description.");
        if (request.Steps.Any(x => x.Expectations is null || x.Expectations.Count == 0)) throw new ArgumentException("Every step requires at least one visible expectation.");
        if (request.Steps.Any(x => x.Action is ValidationAction.Click or ValidationAction.Fill && string.IsNullOrWhiteSpace(x.Target?.Selector))) throw new ArgumentException("Click and fill steps require a stable selector fallback for watched replay.");
    }
}

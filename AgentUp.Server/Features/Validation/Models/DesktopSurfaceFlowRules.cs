using AgentUp.Server.Features.Validation.DTOs;

namespace AgentUp.Server.Features.Validation.Models;

public static class DesktopSurfaceFlowRules
{
    public const string ApplicationName = "Desktop";

    public static string? Validate(SaveValidationFlowRequest request)
    {
        if (!IsDesktopApplication(request.Application))
            return "Desktop surface flows use application 'Desktop', not a workspace application.";
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120)
            return "Name is required and cannot exceed 120 characters.";
        if (request.Description is null)
            return "Description is required.";
        if (!DesktopSurfaceRoutes.IsNamedRoute(request.InitialPath))
            return "Desktop InitialPath must be a named surface route such as workspaces/list or workspace/git.";
        if (request.Steps is null || request.Steps.Count is < 1 or > 200)
            return "A flow requires 1 to 200 steps.";
        if (request.InitialExpectations is null || request.InitialExpectations.Count == 0)
            return "The initial place requires at least one visible expectation.";
        if (HasForbiddenLocator(request.InitialExpectations) || request.Steps.Any(step => HasForbiddenLocator(step)))
            return "Desktop surface flows reject Selector, X, and Y locators. Address controls with testId (AutomationId), name, label, role, or text.";
        if (HasUrlExpectation(request.InitialExpectations) || request.Steps.Any(step => HasUrlExpectation(step.Expectations)))
            return "Url expectations are invalid on a Desktop surface flow.";
        if (request.Steps.Any(step => string.IsNullOrWhiteSpace(step.Description)))
            return "Every step requires a GUI-level description.";
        if (request.Steps.Any(step => step.Expectations is null || step.Expectations.Count == 0))
            return "Every step requires at least one visible expectation.";
        if (request.Steps.Any(step => step.Action == ValidationAction.Navigate && !DesktopSurfaceRoutes.IsNamedRoute(step.Value)))
            return "Desktop Navigate steps require a named surface route such as workspaces/list or workspace/git.";
        if (request.Steps.Any(step => step.Action is ValidationAction.Click or ValidationAction.Fill && !HasSemanticLocator(step.Target)))
            return "Desktop click and fill steps require a testId (AutomationId), name, label, role, or text locator.";
        return null;
    }

    public static bool IsDesktopApplication(string? application) =>
        string.IsNullOrWhiteSpace(application)
        || string.Equals(application.Trim(), ApplicationName, StringComparison.OrdinalIgnoreCase);

    private static bool HasForbiddenLocator(IReadOnlyList<ValidationAssertion>? assertions) =>
        assertions is not null && assertions.Any(assertion => HasForbiddenLocator(assertion.Target));

    private static bool HasForbiddenLocator(ValidationStep step) =>
        HasForbiddenLocator(step.Target) || HasForbiddenLocator(step.Expectations);

    private static bool HasForbiddenLocator(ValidationTarget? target) =>
        target is not null && (target.Selector is not null || target.X is not null || target.Y is not null);

    private static bool HasUrlExpectation(IReadOnlyList<ValidationAssertion>? assertions) =>
        assertions is not null && assertions.Any(assertion => assertion.Kind == ValidationExpectation.Url);

    private static bool HasSemanticLocator(ValidationTarget? target) =>
        target is not null
        && (HasText(target.TestId) || HasText(target.Name) || HasText(target.Label) || HasText(target.Role) || HasText(target.Text));

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
}

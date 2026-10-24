using AgentUp.Server.Features.Validation.DTOs;

namespace AgentUp.Server.Features.Validation.Models;

public static class MobileSurfaceFlowRules
{
    public const string ApplicationName = "Mobile";

    public static string? Validate(SaveValidationFlowRequest request)
    {
        if (!IsMobileApplication(request.Application))
            return "Mobile surface flows use application 'Mobile', not a workspace application.";
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120)
            return "Name is required and cannot exceed 120 characters.";
        if (request.Description is null)
            return "Description is required.";
        if (!MobileSurfaceRoutes.IsExpoRoute(request.InitialPath))
            return "Mobile InitialPath must be an expo-router path such as /connect or /workspace/{workspaceId}/agents.";
        if (request.Steps is null || request.Steps.Count is < 1 or > 200)
            return "A flow requires 1 to 200 steps.";
        if (request.InitialExpectations is null || request.InitialExpectations.Count == 0)
            return "The initial place requires at least one visible expectation.";
        if (HasForbiddenLocator(request.InitialExpectations) || request.Steps.Any(HasForbiddenLocator))
            return "Mobile surface flows reject Selector, X, and Y locators. Address controls with role, label, name, text, or testId.";
        if (request.Steps.Any(step => string.IsNullOrWhiteSpace(step.Description)))
            return "Every step requires a GUI-level description.";
        if (request.Steps.Any(step => step.Expectations is null || step.Expectations.Count == 0))
            return "Every step requires at least one visible expectation.";
        if (request.Steps.Any(step => step.Action == ValidationAction.Navigate && !MobileSurfaceRoutes.IsExpoRoute(step.Value)))
            return "Mobile Navigate steps require an expo-router path such as /connect or /workspace/{workspaceId}/git.";
        if (request.Steps.Any(step => step.Action is ValidationAction.Click or ValidationAction.Fill && !HasSemanticLocator(step.Target)))
            return "Mobile click and fill steps require a role, label, name, text, or testId locator.";
        return null;
    }

    public static bool IsMobileApplication(string? application) =>
        string.IsNullOrWhiteSpace(application)
        || string.Equals(application.Trim(), ApplicationName, StringComparison.OrdinalIgnoreCase);

    private static bool HasForbiddenLocator(IReadOnlyList<ValidationAssertion>? assertions) =>
        assertions is not null && assertions.Any(assertion => HasForbiddenLocator(assertion.Target));

    private static bool HasForbiddenLocator(ValidationStep step) =>
        HasForbiddenLocator(step.Target) || HasForbiddenLocator(step.Expectations);

    private static bool HasForbiddenLocator(ValidationTarget? target) =>
        target is not null && (target.Selector is not null || target.X is not null || target.Y is not null);

    private static bool HasSemanticLocator(ValidationTarget? target) =>
        target is not null
        && (HasText(target.TestId) || HasText(target.Name) || HasText(target.Label) || HasText(target.Role) || HasText(target.Text));

    private static bool HasText(string? value) => !string.IsNullOrWhiteSpace(value);
}

using AgentUp.Server.Features.Validation.DTOs;

namespace AgentUp.Server.Features.Validation.Models;

public static class ValidationFlowAuthoringRules
{
    public static string? SameTargetOnlyVisible(ValidationStep step)
    {
        if (step.Action is not (ValidationAction.Click or ValidationAction.Fill))
            return null;
        if (step.Target is null || step.Expectations is null || step.Expectations.Count == 0)
            return null;
        if (step.Expectations.All(expectation => IsSameControlPresence(expectation, step.Target)))
            return "Every click or fill step must assert a visible consequence, not only that the clicked control still exists.";
        return null;
    }

    private static bool IsSameControlPresence(ValidationAssertion expectation, ValidationTarget target)
    {
        if (expectation.Kind is not (ValidationExpectation.Visible or ValidationExpectation.Enabled))
            return false;
        if (string.Equals(expectation.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase))
            return false;
        return expectation.Target is not null && SameLocator(expectation.Target, target);
    }

    private static bool SameLocator(ValidationTarget left, ValidationTarget right) =>
        Equal(left.TestId, right.TestId)
        && Equal(left.Name, right.Name)
        && Equal(left.Label, right.Label)
        && Equal(left.Role, right.Role)
        && Equal(left.Text, right.Text);

    private static bool Equal(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.Ordinal);
}

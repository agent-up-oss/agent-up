using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Models;

namespace AgentUp.Server.Tests.Features.Validation.Unit;

public sealed class ValidationFlowAuthoringRulesTests
{
    [Test]
    public void SameTargetOnlyVisible_rejectsClickOrFillThatOnlyAssertsTheSameControl()
    {
        var click = Step(
            ValidationAction.Click,
            new(TestId: "login.connect"),
            [new(ValidationExpectation.Visible, "true", new(TestId: "login.connect"))]);
        var fill = Step(
            ValidationAction.Fill,
            new(Name: "Server URL"),
            [new(ValidationExpectation.Enabled, "true", new(Name: "Server URL"))]);

        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(click), Does.Contain("visible consequence"));
        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(fill), Does.Contain("visible consequence"));
    }

    [Test]
    public void SameTargetOnlyVisible_skipsNonMutatingStepsAndIncompleteTargets()
    {
        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(Step(ValidationAction.Navigate)), Is.Null);
        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(Step(ValidationAction.Press)), Is.Null);
        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(Step(ValidationAction.Click)), Is.Null);
        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(Step(ValidationAction.Click, new(TestId: "login.connect"))), Is.Null);
        Assert.That(
            ValidationFlowAuthoringRules.SameTargetOnlyVisible(Step(ValidationAction.Fill, new(TestId: "login.url"), [])),
            Is.Null);
    }

    [Test]
    public void SameTargetOnlyVisible_allowsHiddenOrDisabledOnTheSameControl()
    {
        var hidden = Step(
            ValidationAction.Click,
            new(TestId: "login.connect"),
            [new(ValidationExpectation.Visible, "false", new(TestId: "login.connect"))]);
        var disabled = Step(
            ValidationAction.Fill,
            new(TestId: "login.connect"),
            [new(ValidationExpectation.Enabled, " FALSE ", new(TestId: "login.connect"))]);

        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(hidden), Is.Null);
        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(disabled), Is.Null);
    }

    [Test]
    public void SameTargetOnlyVisible_allowsADifferentControlKindOrMixedConsequence()
    {
        var otherControl = Step(
            ValidationAction.Click,
            new(TestId: "login.connect"),
            [new(ValidationExpectation.Visible, "true", new(TestId: "workspace.list"))]);
        var urlKind = Step(
            ValidationAction.Click,
            new(TestId: "login.connect"),
            [new(ValidationExpectation.Url, "/", new(TestId: "login.connect"))]);
        var missingExpectationTarget = Step(
            ValidationAction.Fill,
            new(Label: "Server URL"),
            [new(ValidationExpectation.Visible, "true")]);
        var mixed = Step(
            ValidationAction.Click,
            new(TestId: "login.connect"),
            [
                new(ValidationExpectation.Visible, "true", new(TestId: "login.connect")),
                new(ValidationExpectation.Visible, "true", new(TestId: "workspace.list"))
            ]);

        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(otherControl), Is.Null);
        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(urlKind), Is.Null);
        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(missingExpectationTarget), Is.Null);
        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(mixed), Is.Null);
    }

    [Test]
    public void SameTargetOnlyVisible_trimsLocatorFieldsAndRequiresEveryLocatorToMatch()
    {
        var trimmed = Step(
            ValidationAction.Click,
            new(TestId: " login.connect "),
            [new(ValidationExpectation.Visible, "true", new(TestId: "login.connect"))]);
        var nameMismatch = Step(
            ValidationAction.Click,
            new(TestId: "login.connect", Name: "Connect"),
            [new(ValidationExpectation.Visible, "true", new(TestId: "login.connect"))]);
        var semanticMatch = Step(
            ValidationAction.Fill,
            new(Label: " Server URL ", Role: "textbox", Text: "http://127.0.0.1:5001"),
            [
                new(
                    ValidationExpectation.Enabled,
                    "true",
                    new(Label: "Server URL", Role: "textbox", Text: "http://127.0.0.1:5001"))
            ]);

        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(trimmed), Does.Contain("visible consequence"));
        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(nameMismatch), Is.Null);
        Assert.That(ValidationFlowAuthoringRules.SameTargetOnlyVisible(semanticMatch), Does.Contain("visible consequence"));
    }

    private static ValidationStep Step(
        ValidationAction action,
        ValidationTarget? target = null,
        IReadOnlyList<ValidationAssertion>? expectations = null) =>
        new("step", "Act", action, target, action == ValidationAction.Fill ? "value" : null, expectations);
}

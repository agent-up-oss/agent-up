using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Models;

namespace AgentUp.Server.Tests.Features.Validation.Unit;

public sealed class MobileSurfaceFlowRulesTests
{
    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase("  ", true)]
    [TestCase("Mobile", true)]
    [TestCase("mobile", true)]
    [TestCase(" MOBILE ", true)]
    [TestCase("web", false)]
    public void IsMobileApplication_treatsBlankAndMobileAsTheSurface(string? application, bool expected) =>
        Assert.That(MobileSurfaceFlowRules.IsMobileApplication(application), Is.EqualTo(expected));

    [Test]
    public void Validate_acceptsAnExpoRouteAndRoleLocator() =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request()), Is.Null);

    [TestCase(ValidationAction.Click, "workspace-tab-git", null, null, null, null)]
    [TestCase(ValidationAction.Fill, null, "Server URL", null, null, null)]
    [TestCase(ValidationAction.Click, null, null, "Server URL", null, null)]
    [TestCase(ValidationAction.Click, null, null, null, "button", null)]
    [TestCase(ValidationAction.Fill, null, null, null, null, "Try and save")]
    public void Validate_acceptsEachSemanticLocator(
        ValidationAction action,
        string? testId,
        string? name,
        string? label,
        string? role,
        string? text) =>
        Assert.That(
            MobileSurfaceFlowRules.Validate(Request(steps:
            [
                Step(
                    action: action,
                    target: new(Role: role, Name: name, Label: label, TestId: testId, Text: text),
                    value: action == ValidationAction.Fill ? "http://127.0.0.1:5001" : null)
            ])),
            Is.Null);

    [Test]
    public void Validate_acceptsAUrlExpectation() =>
        Assert.That(
            MobileSurfaceFlowRules.Validate(Request(initialExpectations: [new(ValidationExpectation.Url, "/connect")])),
            Is.Null);

    [Test]
    public void Validate_acceptsNavigateBetweenExpoRoutes() =>
        Assert.That(
            MobileSurfaceFlowRules.Validate(Request(steps: [Step(action: ValidationAction.Navigate, value: MobileSurfaceRoutes.WorkspaceAgents)])),
            Is.Null);

    [Test]
    public void Validate_rejectsAWorkspaceApplication() =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request(application: "web")), Does.Contain("application 'Mobile'"));

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_rejectsAMissingName(string name) =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request(name: name)), Does.Contain("Name is required"));

    [Test]
    public void Validate_rejectsANameOver120Characters() =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request(name: new string('a', 121))), Does.Contain("Name is required"));

    [Test]
    public void Validate_rejectsAMissingDescription() =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request(description: null)), Does.Contain("Description is required"));

    [Test]
    public void Validate_rejectsAnUnknownInitialPath() =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request(initialPath: "workspace/git")), Does.Contain("expo-router path"));

    [Test]
    public void Validate_rejectsAMissingStepList() =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request(includeSteps: false)), Does.Contain("1 to 200 steps"));

    [Test]
    public void Validate_rejectsAnEmptyStepList() =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request(steps: [])), Does.Contain("1 to 200 steps"));

    [Test]
    public void Validate_rejectsMoreThan200Steps() =>
        Assert.That(
            MobileSurfaceFlowRules.Validate(Request(steps: Enumerable.Range(0, 201).Select(_ => Step()).ToList())),
            Does.Contain("1 to 200 steps"));

    [Test]
    public void Validate_rejectsMissingInitialExpectations() =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request(includeExpectations: false)), Does.Contain("initial place"));

    [Test]
    public void Validate_rejectsEmptyInitialExpectations() =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request(initialExpectations: [])), Does.Contain("initial place"));

    [Test]
    public void Validate_rejectsSelectorOnAnInitialExpectation() =>
        Assert.That(
            MobileSurfaceFlowRules.Validate(Request(initialExpectations: [new(ValidationExpectation.Visible, "true", new(Selector: "#git"))])),
            Does.Contain("reject Selector, X, and Y"));

    [Test]
    public void Validate_rejectsCoordinatesOnAStepTarget() =>
        Assert.That(
            MobileSurfaceFlowRules.Validate(Request(steps: [Step(target: new(Name: "Git", X: 8, Y: 12))])),
            Does.Contain("reject Selector, X, and Y"));

    [Test]
    public void Validate_rejectsSelectorOnAStepExpectation() =>
        Assert.That(
            MobileSurfaceFlowRules.Validate(Request(steps: [Step(expectations: [new(ValidationExpectation.Visible, "true", new(Selector: "#ok"))])])),
            Does.Contain("reject Selector, X, and Y"));

    [Test]
    public void Validate_rejectsABlankStepDescription() =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request(steps: [Step(description: " ")])), Does.Contain("GUI-level description"));

    [Test]
    public void Validate_rejectsAStepWithoutExpectations() =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request(steps: [Step(includeExpectations: false)])), Does.Contain("at least one visible expectation"));

    [Test]
    public void Validate_rejectsNavigateWithoutAnExpoRoute() =>
        Assert.That(
            MobileSurfaceFlowRules.Validate(Request(steps: [Step(action: ValidationAction.Navigate, value: "workspace/git")])),
            Does.Contain("expo-router path"));

    [Test]
    public void Validate_rejectsClickWithoutASemanticLocator() =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request(steps: [Step(target: new())])), Does.Contain("role, label, name, text, or testId"));

    [Test]
    public void Validate_rejectsAWhitespaceOnlyLocator() =>
        Assert.That(MobileSurfaceFlowRules.Validate(Request(steps: [Step(target: new(TestId: "  "))])), Does.Contain("role, label, name, text, or testId"));

    private static SaveValidationFlowRequest Request(
        string application = MobileSurfaceFlowRules.ApplicationName,
        string name = "Open Agents",
        string? description = "The Agents tab is visible.",
        string initialPath = MobileSurfaceRoutes.WorkspaceAgents,
        IReadOnlyList<ValidationAssertion>? initialExpectations = null,
        IReadOnlyList<ValidationStep>? steps = null,
        bool includeSteps = true,
        bool includeExpectations = true) =>
        new(
            null,
            application,
            name,
            description!,
            initialPath,
            includeExpectations ? initialExpectations ?? [new(ValidationExpectation.Visible, "true")] : null,
            includeSteps ? steps ?? [Step()] : null!,
            ValidationSurface.Mobile);

    private static ValidationStep Step(
        string description = "Open the Agents tab",
        ValidationAction action = ValidationAction.Click,
        ValidationTarget? target = null,
        string? value = null,
        IReadOnlyList<ValidationAssertion>? expectations = null,
        bool includeExpectations = true) =>
        new(
            "step",
            description,
            action,
            action is ValidationAction.Click or ValidationAction.Fill ? target ?? new(Role: "tab", Name: "Agents") : target,
            value,
            includeExpectations ? expectations ?? [new(ValidationExpectation.Visible, "true")] : null);
}

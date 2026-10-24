using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Models;

namespace AgentUp.Server.Tests.Features.Validation.Unit;

public sealed class DesktopSurfaceFlowRulesTests
{
    [TestCase(null, true)]
    [TestCase("", true)]
    [TestCase("  ", true)]
    [TestCase("Desktop", true)]
    [TestCase("desktop", true)]
    [TestCase(" DESKTOP ", true)]
    [TestCase("web", false)]
    public void IsDesktopApplication_treatsBlankAndDesktopAsTheSurface(string? application, bool expected) =>
        Assert.That(DesktopSurfaceFlowRules.IsDesktopApplication(application), Is.EqualTo(expected));

    [Test]
    public void Validate_acceptsANamedRouteAndTestId() =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request()), Is.Null);

    [TestCase(ValidationAction.Click, "git.commit", null, null, null, null)]
    [TestCase(ValidationAction.Fill, null, "Commit", null, null, null)]
    [TestCase(ValidationAction.Click, null, null, "Message", null, null)]
    [TestCase(ValidationAction.Click, null, null, null, "button", null)]
    [TestCase(ValidationAction.Fill, null, null, null, null, "Commit")]
    public void Validate_acceptsEachSemanticLocator(
        ValidationAction action,
        string? testId,
        string? name,
        string? label,
        string? role,
        string? text) =>
        Assert.That(
            DesktopSurfaceFlowRules.Validate(Request(steps:
            [
                Step(
                    action: action,
                    target: new(Role: role, Name: name, Label: label, TestId: testId, Text: text),
                    value: action == ValidationAction.Fill ? "msg" : null)
            ])),
            Is.Null);

    [Test]
    public void Validate_acceptsNavigateBetweenNamedRoutes() =>
        Assert.That(
            DesktopSurfaceFlowRules.Validate(Request(steps: [Step(action: ValidationAction.Navigate, value: DesktopSurfaceRoutes.WorkspaceAgents)])),
            Is.Null);

    [Test]
    public void Validate_rejectsAWorkspaceApplication() =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request(application: "web")), Does.Contain("application 'Desktop'"));

    [TestCase("")]
    [TestCase("   ")]
    public void Validate_rejectsAMissingName(string name) =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request(name: name)), Does.Contain("Name is required"));

    [Test]
    public void Validate_rejectsANameOver120Characters() =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request(name: new string('a', 121))), Does.Contain("Name is required"));

    [Test]
    public void Validate_rejectsAMissingDescription() =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request(description: null)), Does.Contain("Description is required"));

    [Test]
    public void Validate_rejectsAnUnknownInitialPath() =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request(initialPath: "/git")), Does.Contain("named surface route"));

    [Test]
    public void Validate_rejectsAMissingStepList() =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request(includeSteps: false)), Does.Contain("1 to 200 steps"));

    [Test]
    public void Validate_rejectsAnEmptyStepList() =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request(steps: [])), Does.Contain("1 to 200 steps"));

    [Test]
    public void Validate_rejectsMoreThan200Steps() =>
        Assert.That(
            DesktopSurfaceFlowRules.Validate(Request(steps: Enumerable.Range(0, 201).Select(_ => Step()).ToList())),
            Does.Contain("1 to 200 steps"));

    [Test]
    public void Validate_rejectsMissingInitialExpectations() =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request(includeExpectations: false)), Does.Contain("initial place"));

    [Test]
    public void Validate_rejectsEmptyInitialExpectations() =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request(initialExpectations: [])), Does.Contain("initial place"));

    [Test]
    public void Validate_rejectsSelectorOnAnInitialExpectation() =>
        Assert.That(
            DesktopSurfaceFlowRules.Validate(Request(initialExpectations: [new(ValidationExpectation.Visible, "true", new(Selector: "#git"))])),
            Does.Contain("reject Selector, X, and Y"));

    [Test]
    public void Validate_rejectsCoordinatesOnAStepTarget() =>
        Assert.That(
            DesktopSurfaceFlowRules.Validate(Request(steps: [Step(target: new(Name: "Commit", X: 8, Y: 12))])),
            Does.Contain("reject Selector, X, and Y"));

    [Test]
    public void Validate_rejectsSelectorOnAStepExpectation() =>
        Assert.That(
            DesktopSurfaceFlowRules.Validate(Request(steps: [Step(expectations: [new(ValidationExpectation.Visible, "true", new(Selector: "#ok"))])])),
            Does.Contain("reject Selector, X, and Y"));

    [Test]
    public void Validate_rejectsAUrlOnTheInitialPlace() =>
        Assert.That(
            DesktopSurfaceFlowRules.Validate(Request(initialExpectations: [new(ValidationExpectation.Url, "/")])),
            Does.Contain("Url expectations are invalid"));

    [Test]
    public void Validate_rejectsAUrlOnAStep() =>
        Assert.That(
            DesktopSurfaceFlowRules.Validate(Request(steps: [Step(expectations: [new(ValidationExpectation.Url, "/")])])),
            Does.Contain("Url expectations are invalid"));

    [Test]
    public void Validate_rejectsABlankStepDescription() =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request(steps: [Step(description: " ")])), Does.Contain("GUI-level description"));

    [Test]
    public void Validate_rejectsAStepWithoutExpectations() =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request(steps: [Step(includeExpectations: false)])), Does.Contain("at least one visible expectation"));

    [Test]
    public void Validate_rejectsNavigateWithoutANamedRoute() =>
        Assert.That(
            DesktopSurfaceFlowRules.Validate(Request(steps: [Step(action: ValidationAction.Navigate, value: "/git")])),
            Does.Contain("named surface route"));

    [Test]
    public void Validate_rejectsClickWithoutASemanticLocator() =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request(steps: [Step(target: new())])), Does.Contain("testId (AutomationId)"));

    [Test]
    public void Validate_rejectsAWhitespaceOnlyLocator() =>
        Assert.That(DesktopSurfaceFlowRules.Validate(Request(steps: [Step(target: new(TestId: "  "))])), Does.Contain("testId (AutomationId)"));

    [Test]
    public void Validate_rejectsAClickWhoseOnlyExpectationIsTheSameControl() =>
        Assert.That(
            DesktopSurfaceFlowRules.Validate(Request(steps:
            [
                Step(
                    target: new(TestId: "login.connect"),
                    expectations: [new(ValidationExpectation.Visible, "true", new(TestId: "login.connect"))])
            ])),
            Does.Contain("visible consequence"));

    [Test]
    public void Validate_acceptsAClickThatAssertsADifferentConsequence() =>
        Assert.That(
            DesktopSurfaceFlowRules.Validate(Request(steps:
            [
                Step(
                    target: new(TestId: "login.connect"),
                    expectations: [new(ValidationExpectation.Visible, "true", new(TestId: "workspace.list"))])
            ])),
            Is.Null);

    private static SaveValidationFlowRequest Request(
        string application = DesktopSurfaceFlowRules.ApplicationName,
        string name = "Open Git",
        string? description = "The Git tab is visible.",
        string initialPath = DesktopSurfaceRoutes.WorkspaceGit,
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
            ValidationSurface.Desktop);

    private static ValidationStep Step(
        string description = "Commit selected files",
        ValidationAction action = ValidationAction.Click,
        ValidationTarget? target = null,
        string? value = null,
        IReadOnlyList<ValidationAssertion>? expectations = null,
        bool includeExpectations = true) =>
        new(
            "step",
            description,
            action,
            action is ValidationAction.Click or ValidationAction.Fill ? target ?? new(TestId: "git.commit") : target,
            value,
            includeExpectations ? expectations ?? [new(ValidationExpectation.Visible, "true")] : null);
}

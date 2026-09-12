using System.Reactive.Linq;
using AgentUp.Desktop.Features.Validation.DTOs;
using AgentUp.Desktop.Features.Validation.Models;
using AgentUp.Desktop.Features.Validation.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Validation.Unit;

// The validation panel reports progress purely through these three view models, so the glyph and
// colour a viewer sees for each state is the observable contract, not an implementation detail.
public sealed class ValidationRunStateViewModelTests
{
    [TestCase(ValidationRunState.Pending, "○", "#8a9a92")]
    [TestCase(ValidationRunState.Running, "●", "#e0a128")]
    [TestCase(ValidationRunState.Passed, "✓", "#2bf27a")]
    [TestCase(ValidationRunState.Failed, "✗", "#d84f4f")]
    public void Check_showsAGlyphAndColourForEveryRunState(ValidationRunState state, string glyph, string color)
    {
        var check = new ValidationCheckViewModel("Text contains \"Cart\"");
        Move(check, state);

        Assert.Multiple(() =>
        {
            Assert.That(check.State, Is.EqualTo(state));
            Assert.That(check.StatusGlyph, Is.EqualTo(glyph));
            Assert.That(check.StatusColor, Is.EqualTo(color));
        });
    }

    [TestCase(ValidationRunState.Pending, "○", "#8a9a92")]
    [TestCase(ValidationRunState.Running, "●", "#e0a128")]
    [TestCase(ValidationRunState.Passed, "✓", "#2bf27a")]
    [TestCase(ValidationRunState.Failed, "✗", "#d84f4f")]
    public void Stage_showsAGlyphAndColourForEveryRunState(ValidationRunState state, string glyph, string color)
    {
        var stage = Stage();
        Move(stage, state);

        Assert.Multiple(() =>
        {
            Assert.That(stage.State, Is.EqualTo(state));
            Assert.That(stage.StatusGlyph, Is.EqualTo(glyph));
            Assert.That(stage.StatusColor, Is.EqualTo(color));
        });
    }

    [Test]
    public void Check_raisesTheDerivedPropertiesWhenTheStateChanges()
    {
        var check = new ValidationCheckViewModel("Text contains \"Cart\"");
        List<string?> raised = [];
        check.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        check.SetPassed();

        Assert.That(raised, Is.EquivalentTo(new[] { nameof(check.State), nameof(check.StatusGlyph), nameof(check.StatusColor) }));
    }

    [Test]
    public void Stage_describesEachAssertionKindItWasBuiltFrom()
    {
        var stage = new ValidationStageViewModel("s1", "Starting page",
        [
            new ValidationAssertionDto(ValidationExpectationDto.Text, "Cart"),
            new ValidationAssertionDto(ValidationExpectationDto.Url, "/cart"),
            new ValidationAssertionDto(ValidationExpectationDto.Title, "Shop"),
            new ValidationAssertionDto(ValidationExpectationDto.Visible, "ignored", new ValidationTargetDto(Selector: "#cart")),
            new ValidationAssertionDto(ValidationExpectationDto.Visible, "fallback")
        ]);

        Assert.That(stage.Checks.Select(x => x.Label), Is.EqualTo(new[]
        {
            "Text contains \"Cart\"",
            "URL is /cart",
            "Title is \"Shop\"",
            "Visible: #cart",
            "fallback"
        }));
    }

    [Test]
    public void Stage_resetReturnsItselfAndEveryCheckToPending()
    {
        var stage = Stage();
        stage.SetFailed();
        stage.GetCheck(0)!.SetPassed();

        stage.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(stage.State, Is.EqualTo(ValidationRunState.Pending));
            Assert.That(stage.Checks.Select(x => x.State), Is.All.EqualTo(ValidationRunState.Pending));
        });
    }

    [Test]
    public void Stage_getCheckIsBoundsSafe()
    {
        var stage = Stage();

        Assert.Multiple(() =>
        {
            Assert.That(stage.GetCheck(0), Is.Not.Null);
            Assert.That(stage.GetCheck(-1), Is.Null);
            Assert.That(stage.GetCheck(stage.Checks.Count), Is.Null);
        });
    }

    [Test]
    public void Flow_buildsAStartingStageAheadOfOneStagePerStep()
    {
        var item = Item();

        Assert.Multiple(() =>
        {
            Assert.That(item.Stages.Select(x => x.Id), Is.EqualTo(new[] { "__initial__", "click", "type" }));
            Assert.That(item.Stages[0].Title, Is.EqualTo("Starting page"));
            Assert.That(item.Stages[1].Title, Is.EqualTo("Click the button"));
            // The second step declares no expectations, so its stage carries no checks.
            Assert.That(item.Stages[2].Checks, Is.Empty);
        });
    }

    [Test]
    public void Flow_surfacesTheUnderlyingFlowFields()
    {
        var item = Item();

        Assert.Multiple(() =>
        {
            Assert.That(item.Name, Is.EqualTo("Checkout"));
            Assert.That(item.Description, Is.EqualTo("A shopper checks out"));
            Assert.That(item.InitialPath, Is.EqualTo("/cart"));
        });
    }

    [Test]
    public void Flow_beginFlowExpandsClearsTheLastResultAndResetsEveryStage()
    {
        var item = Item();
        item.CompleteStage("click", true);
        item.CompleteFlow(false, "an earlier failure");

        item.BeginFlow();

        Assert.Multiple(() =>
        {
            Assert.That(item.IsExpanded, Is.True);
            Assert.That(item.RunState, Is.EqualTo(ValidationRunState.Running));
            Assert.That(item.ResultMessage, Is.Null);
            Assert.That(item.HasRunResult, Is.False);
            Assert.That(item.Stages.Select(x => x.State), Is.All.EqualTo(ValidationRunState.Pending));
        });
    }

    [Test]
    public void Flow_reportsProgressThroughToTheIndividualChecks()
    {
        var item = Item();

        item.BeginStage("click");
        item.BeginCheck("click", 0);

        Assert.Multiple(() =>
        {
            Assert.That(item.Stages[1].State, Is.EqualTo(ValidationRunState.Running));
            Assert.That(item.Stages[1].GetCheck(0)!.State, Is.EqualTo(ValidationRunState.Running));
        });

        item.CompleteCheck("click", 0, passed: true);
        item.CompleteStage("click", passed: true);

        Assert.Multiple(() =>
        {
            Assert.That(item.Stages[1].GetCheck(0)!.State, Is.EqualTo(ValidationRunState.Passed));
            Assert.That(item.Stages[1].State, Is.EqualTo(ValidationRunState.Passed));
        });
    }

    [Test]
    public void Flow_recordsAFailedCheckAndStage()
    {
        var item = Item();

        item.CompleteCheck("click", 0, passed: false, error: "not found");
        item.CompleteStage("click", passed: false, error: "not found");

        Assert.Multiple(() =>
        {
            Assert.That(item.Stages[1].GetCheck(0)!.State, Is.EqualTo(ValidationRunState.Failed));
            Assert.That(item.Stages[1].State, Is.EqualTo(ValidationRunState.Failed));
        });
    }

    // A replay reports against stage ids that came from the flow; an id or index that does not
    // resolve must be dropped rather than throwing out of the reporter and killing the run.
    [Test]
    public void Flow_ignoresProgressForAStageOrCheckItDoesNotHave()
    {
        var item = Item();

        Assert.DoesNotThrow(() =>
        {
            item.BeginStage("nope");
            item.BeginCheck("nope", 0);
            item.BeginCheck("click", 99);
            item.CompleteCheck("nope", 0, true);
            item.CompleteCheck("click", 99, true);
            item.CompleteStage("nope", true);
        });

        Assert.That(item.Stages.Select(x => x.State), Is.All.EqualTo(ValidationRunState.Pending));
    }

    [TestCase(true, ValidationRunState.Passed)]
    [TestCase(false, ValidationRunState.Failed)]
    public void Flow_completeFlowRecordsTheOutcomeAndMessage(bool passed, ValidationRunState expected)
    {
        var item = Item();

        item.CompleteFlow(passed, "the message");

        Assert.Multiple(() =>
        {
            Assert.That(item.RunState, Is.EqualTo(expected));
            Assert.That(item.ResultMessage, Is.EqualTo("the message"));
            Assert.That(item.HasRunResult, Is.True);
        });
    }

    // Deliberately exercises the succeeding path only: a faulted ReactiveCommand routes to
    // RxApp.DefaultExceptionHandler, which rethrows on the scheduler and can take down unrelated
    // tests in this assembly. The finally is still covered by the IsRunning assertion below.
    [Test]
    public async Task Flow_runCommandFlagsIsRunningAroundTheReplay()
    {
        var duringRun = false;
        var item = Item(run: vm =>
        {
            duringRun = vm.IsRunning;
            return Task.CompletedTask;
        });

        await item.RunCommand.Execute().FirstAsync();

        Assert.Multiple(() =>
        {
            Assert.That(duringRun, Is.True, "IsRunning should be set before the replay starts");
            Assert.That(item.IsRunning, Is.False, "the finally must clear IsRunning once the replay returns");
        });
    }

    private static void Move(ValidationCheckViewModel check, ValidationRunState state)
    {
        if (state is ValidationRunState.Running) check.SetRunning();
        else if (state is ValidationRunState.Passed) check.SetPassed();
        else if (state is ValidationRunState.Failed) check.SetFailed();
        else check.Reset();
    }

    private static void Move(ValidationStageViewModel stage, ValidationRunState state)
    {
        if (state is ValidationRunState.Running) stage.SetRunning();
        else if (state is ValidationRunState.Passed) stage.SetPassed();
        else if (state is ValidationRunState.Failed) stage.SetFailed();
        else stage.Reset();
    }

    private static ValidationStageViewModel Stage() =>
        new("s1", "Starting page", [new ValidationAssertionDto(ValidationExpectationDto.Text, "Cart")]);

    private static ValidationFlowItemViewModel Item(Func<ValidationFlowItemViewModel, Task>? run = null) =>
        new(
            new ValidationFlowDto("flow", "ws", "shop", "Checkout", "A shopper checks out", "/cart",
                [new ValidationAssertionDto(ValidationExpectationDto.Text, "Cart")],
                [
                    new ValidationStepDto("click", "Click the button", ValidationActionDto.Click,
                        new ValidationTargetDto(Selector: "#buy"), null,
                        [new ValidationAssertionDto(ValidationExpectationDto.Text, "Done")]),
                    new ValidationStepDto("type", "Type the email", ValidationActionDto.Fill,
                        new ValidationTargetDto(Selector: "#email"), "a@b.test")
                ],
                1),
            run ?? (_ => Task.CompletedTask));
}

using AgentUp.Server.Features.Applications.DTOs;
using AgentUp.Server.Features.Validation.DTOs;
using AgentUp.Server.Features.Validation.Interfaces;
using AgentUp.Server.Features.Validation.Providers;
using AgentUp.Server.Features.Validation.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Fake;
namespace AgentUp.Server.Tests.Features.Validation.Unit;
public sealed class ValidationFlowServiceTests
{
    [TestCase("//outside.example/path")]
    [TestCase("https://outside.example/path")]
    public async Task Save_rejects_navigation_that_can_escape_the_application_origin(string path)
    {
        var (service, workspaceId) = await CreateAsync();

        var result = await service.SaveAsync(workspaceId, Request(initialPath: path));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("local application-relative path"));
        });
    }

    [Test]
    public async Task Save_storesTheFlow_withATrimmedIdentityAndFirstVersion()
    {
        var (service, workspaceId) = await CreateAsync();

        var result = await service.SaveAsync(workspaceId, Request(name: "  Checkout  ", description: "  Buys a thing  "));

        Assert.That(result.Succeeded, Is.True, result.Error);
        Assert.Multiple(() =>
        {
            Assert.That(result.Flow!.Name, Is.EqualTo("Checkout"));
            Assert.That(result.Flow.Description, Is.EqualTo("Buys a thing"));
            Assert.That(result.Flow.Version, Is.EqualTo(1));
            Assert.That(result.Flow.Id, Is.Not.Empty);
            Assert.That(result.Flow.WorkspaceId, Is.EqualTo(workspaceId));
        });
    }

    [Test]
    public async Task Save_bumpsTheVersion_whenAnExistingFlowIsReplaced()
    {
        var (service, workspaceId) = await CreateAsync();
        var created = await service.SaveAsync(workspaceId, Request());
        Assert.That(created.Succeeded, Is.True, created.Error);

        var updated = await service.SaveAsync(workspaceId, Request(id: created.Flow!.Id, name: "Checkout again"));

        Assert.That(updated.Succeeded, Is.True, updated.Error);
        Assert.Multiple(() =>
        {
            Assert.That(updated.Flow!.Id, Is.EqualTo(created.Flow.Id));
            Assert.That(updated.Flow.Version, Is.EqualTo(2));
            Assert.That(updated.Flow.Name, Is.EqualTo("Checkout again"));
        });
    }

    [Test]
    public async Task Save_reportsAnUnknownWorkspace()
    {
        var (service, _) = await CreateAsync();

        var result = await service.SaveAsync("00000000-0000-0000-0000-000000000000", Request());

        Assert.That(result.Error, Does.Contain("Workspace was not found."));
    }

    [Test]
    public async Task Save_reportsAnApplicationThatTheWorkspaceDoesNotHave()
    {
        var (service, workspaceId) = await CreateAsync();

        var result = await service.SaveAsync(workspaceId, Request(application: "not-web"));

        Assert.That(result.Error, Does.Contain("Application was not found"));
    }

    [TestCase("")]
    [TestCase("   ")]
    public async Task Save_requiresAName(string name)
    {
        var (service, workspaceId) = await CreateAsync();

        var result = await service.SaveAsync(workspaceId, Request(name: name));

        Assert.That(result.Error, Does.Contain("Name is required"));
    }

    [Test]
    public async Task Save_rejectsANameLongerThanTheLimit()
    {
        var (service, workspaceId) = await CreateAsync();

        var result = await service.SaveAsync(workspaceId, Request(name: new string('x', 121)));

        Assert.That(result.Error, Does.Contain("cannot exceed 120 characters"));
    }

    // Description and Steps are non-nullable on the record but arrive from JSON, where a missing
    // member lands as null and used to reach Trim() and Count as an unhandled failure.
    [Test]
    public async Task Save_reportsAMissingDescription_ratherThanFailingOnIt()
    {
        var (service, workspaceId) = await CreateAsync();

        var result = await service.SaveAsync(workspaceId, Request(description: null!));

        Assert.That(result.Error, Does.Contain("Description is required."));
    }

    [Test]
    public async Task Save_reportsMissingSteps_ratherThanFailingOnThem()
    {
        var (service, workspaceId) = await CreateAsync();
        // Built inline: the Request helper coalesces a null list, which would hide the very
        // case this covers - a payload that omits "steps" entirely.
        var request = new SaveValidationFlowRequest(
            null, "web", "Checkout", "User checks out", "/cart",
            [new ValidationAssertion(ValidationExpectation.Text, "Cart")], null!);

        var result = await service.SaveAsync(workspaceId, request);

        Assert.That(result.Error, Does.Contain("1 to 200 steps"));
    }

    [Test]
    public async Task Save_requiresAtLeastOneStep()
    {
        var (service, workspaceId) = await CreateAsync();

        var result = await service.SaveAsync(workspaceId, Request(steps: []));

        Assert.That(result.Error, Does.Contain("1 to 200 steps"));
    }

    [Test]
    public async Task Save_rejectsMoreStepsThanTheLimit()
    {
        var (service, workspaceId) = await CreateAsync();
        var steps = Enumerable.Range(0, 201).Select(i => Step(id: $"step{i}")).ToList();

        var result = await service.SaveAsync(workspaceId, Request(steps: steps));

        Assert.That(result.Error, Does.Contain("1 to 200 steps"));
    }

    [Test]
    public async Task Save_requiresAnInitialExpectation()
    {
        var (service, workspaceId) = await CreateAsync();

        var result = await service.SaveAsync(workspaceId, Request(initialExpectations: []));

        Assert.That(result.Error, Does.Contain("initial place requires"));
    }

    [Test]
    public async Task Save_requiresEveryStepToDescribeWhatTheUserSees()
    {
        var (service, workspaceId) = await CreateAsync();

        var result = await service.SaveAsync(workspaceId, Request(steps: [Step(description: "  ")]));

        Assert.That(result.Error, Does.Contain("GUI-level description"));
    }

    [Test]
    public async Task Save_requiresEveryStepToAssertSomething()
    {
        var (service, workspaceId) = await CreateAsync();

        var result = await service.SaveAsync(workspaceId, Request(steps: [Step(expectations: [])]));

        Assert.That(result.Error, Does.Contain("at least one visible expectation"));
    }

    [Test]
    public async Task Save_rejectsANavigateStepThatLeavesTheApplication()
    {
        var (service, workspaceId) = await CreateAsync();
        var step = Step(action: ValidationAction.Navigate, target: null, value: "https://outside.example/");

        var result = await service.SaveAsync(workspaceId, Request(steps: [step]));

        Assert.That(result.Error, Does.Contain("Navigate steps require"));
    }

    [TestCase(ValidationAction.Click)]
    [TestCase(ValidationAction.Fill)]
    public async Task Save_requiresASelectorFallbackForInteractiveSteps(ValidationAction action)
    {
        var (service, workspaceId) = await CreateAsync();
        var step = Step(action: action, target: new ValidationTarget(Role: "button", Name: "Submit"), value: "x");

        var result = await service.SaveAsync(workspaceId, Request(steps: [step]));

        Assert.That(result.Error, Does.Contain("stable selector fallback"));
    }

    [Test]
    public async Task List_returnsOnlyTheApplicationsOwnFlows_orderedByName()
    {
        var (service, workspaceId) = await CreateAsync(secondApplication: "api");
        await service.SaveAsync(workspaceId, Request(name: "Zebra"));
        await service.SaveAsync(workspaceId, Request(name: "alpha"));
        await service.SaveAsync(workspaceId, Request(application: "api", name: "Other"));

        var flows = await service.ListAsync(workspaceId, "web");

        Assert.That(flows.Select(x => x.Name), Is.EqualTo(new[] { "alpha", "Zebra" }));
    }

    [Test]
    public async Task Get_returnsTheSavedFlow_andNullForAnUnknownId()
    {
        var (service, workspaceId) = await CreateAsync();
        var saved = await service.SaveAsync(workspaceId, Request());

        var found = await service.GetAsync(workspaceId, saved.Flow!.Id);
        var missing = await service.GetAsync(workspaceId, "missing");

        Assert.Multiple(() =>
        {
            Assert.That(found?.Name, Is.EqualTo("Checkout"));
            Assert.That(missing, Is.Null);
        });
    }

    [Test]
    public async Task Delete_removesTheFlow_andReportsAnUnknownId()
    {
        var (service, workspaceId) = await CreateAsync();
        var saved = await service.SaveAsync(workspaceId, Request());

        var removed = await service.DeleteAsync(workspaceId, saved.Flow!.Id);
        var again = await service.DeleteAsync(workspaceId, saved.Flow.Id);
        var remaining = await service.ListAsync(workspaceId, "web");

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(again, Is.False);
            Assert.That(remaining, Is.Empty);
        });
    }

    [Test]
    public async Task Export_returnsAPlaywrightSpec_andNullForAnUnknownId()
    {
        var (service, workspaceId) = await CreateAsync();
        var saved = await service.SaveAsync(workspaceId, Request(name: "Checkout works"));

        var export = await service.ExportAsync(workspaceId, saved.Flow!.Id);
        var missing = await service.ExportAsync(workspaceId, "missing");

        Assert.That(export, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(export!.FileName, Is.EqualTo("checkout-works.spec.ts"));
            Assert.That(export.Content, Does.Contain("@playwright/test"));
            Assert.That(missing, Is.Null);
        });
    }

    [Test]
    public async Task Run_reportsAnUnknownFlow_withoutTouchingTheBrowser()
    {
        var (service, workspaceId) = await CreateAsync();

        var result = await service.RunAsync(workspaceId, "missing");

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Is.EqualTo("Validation flow was not found."));
        });
    }

    [Test]
    public async Task Run_reportsAnApplicationWithNoHttpPort_withoutTouchingTheBrowser()
    {
        var (service, workspaceId) = await CreateAsync();
        var saved = await service.SaveAsync(workspaceId, Request());

        var result = await service.RunAsync(workspaceId, saved.Flow!.Id);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("no allocated HTTP port"));
        });
    }

    // BrowserMcpTools is sealed with non-virtual members, so the browser-driven half of RunAsync
    // has no seam; only the two guards above are reachable without a real browser session.
    private static async Task<(ValidationFlowService Service, string WorkspaceId)> CreateAsync(string? secondApplication = null)
    {
        var registry = ServerTestComposition.CreateRegistry();
        List<ApplicationDefinition> applications = [new ApplicationDefinition("web", "npm start", ".")];
        if (secondApplication is not null)
            applications.Add(new ApplicationDefinition(secondApplication, "npm start", "."));

        var workspace = await registry.RegisterAsync(new RegisterWorkspaceRequest("Workspace", "/repo", "/repo", "main", "abc")
        {
            Applications = applications
        });

        var service = new ValidationFlowService(
            new MemoryRepository(), new WorkspaceQueryController(registry), null!, new PlaywrightFlowExporter());
        return (service, workspace.Id);
    }

    private static SaveValidationFlowRequest Request(
        string? id = null,
        string application = "web",
        string name = "Checkout",
        string description = "User checks out",
        string initialPath = "/cart",
        IReadOnlyList<ValidationAssertion>? initialExpectations = null,
        IReadOnlyList<ValidationStep>? steps = null) =>
        new(id, application, name, description, initialPath,
            initialExpectations ?? [new ValidationAssertion(ValidationExpectation.Text, "Cart")],
            steps ?? [Step()]);

    private static ValidationStep Step(
        string id = "submit",
        string description = "Submit the order",
        ValidationAction action = ValidationAction.Click,
        ValidationTarget? target = null,
        string? value = null,
        IReadOnlyList<ValidationAssertion>? expectations = null) =>
        new(id, description, action,
            action is ValidationAction.Click or ValidationAction.Fill
                ? target ?? new ValidationTarget(Role: "button", Name: "Submit", Selector: "#submit")
                : target,
            value,
            expectations ?? [new ValidationAssertion(ValidationExpectation.Text, "Confirmed")]);

    private sealed class MemoryRepository : IValidationFlowRepository
    {
        private readonly Dictionary<string, IReadOnlyList<ValidationFlow>> _flows = [];

        public Task<IReadOnlyList<ValidationFlow>> LoadAsync(string workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ValidationFlow>>(
                _flows.TryGetValue(workspaceId, out var flows) ? flows : []);

        public Task SaveAsync(string workspaceId, IReadOnlyList<ValidationFlow> flows, CancellationToken cancellationToken = default)
        {
            _flows[workspaceId] = flows;
            return Task.CompletedTask;
        }
    }
}

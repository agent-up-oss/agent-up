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
        var registry = ServerTestComposition.CreateRegistry();
        var workspace = await registry.RegisterAsync(new RegisterWorkspaceRequest("Workspace", "/repo", "/repo", "main", "abc")
        {
            Applications = [new ApplicationDefinition("web", "npm start", ".")]
        });
        var service = new ValidationFlowService(new MemoryRepository(), new WorkspaceQueryController(registry), null!, new PlaywrightFlowExporter());
        var request = new SaveValidationFlowRequest(null, "web", "Checkout", "User checks out", path,
            [new(ValidationExpectation.Text, "Cart")],
            [new("submit", "Submit the order", ValidationAction.Click, new(Role: "button", Name: "Submit", Selector: "#submit"), Expectations: [new(ValidationExpectation.Text, "Confirmed")])]);

        var result = await service.SaveAsync(workspace.Id, request);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Error, Does.Contain("local application-relative path"));
        });
    }

    private sealed class MemoryRepository : IValidationFlowRepository
    {
        public Task<IReadOnlyList<ValidationFlow>> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ValidationFlow>>([]);
        public Task SaveAsync(IReadOnlyList<ValidationFlow> flows, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

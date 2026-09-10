using AgentUp.Server.Features.Orchestration.Controllers;
using AgentUp.Server.Features.Orchestration.DTOs;
using AgentUp.Server.Features.Orchestration.Services;
using AgentUp.Server.Features.SourceClones.Controllers;
using AgentUp.Server.Features.SourceClones.DTOs;
using AgentUp.Server.Features.SourceClones.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Tests.Fake;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AgentUp.Server.Tests.Features.SourceClones.Controller;

[TestFixture]
public sealed class SourceClonesControllerTests
{
    [Test]
    public void GetRoot_returnsTheConfiguredSourceClonesRoot()
    {
        var controller = CreateController(new FakeSourceCloneGitProvider());

        var result = controller.GetRoot();

        Assert.That(result.Value, Is.EqualTo(new SourceCloneRoot("/clones")));
    }

    [Test]
    public async Task Clone_returnsCreatedWithTheRegisteredWorkspace()
    {
        var git = new FakeSourceCloneGitProvider();
        var controller = CreateController(git);

        var result = await controller.Clone(new CloneSourceRequest("https://example.test/acme/widgets.git", "main"));

        var created = result as CreatedResult
            ?? throw new AssertionException($"Expected a 201 Created result, got {result.GetType().Name}.");
        var workspace = created.Value as Workspace
            ?? throw new AssertionException("Expected the created result to carry the registered workspace.");
        var cloned = git.Cloned
            ?? throw new AssertionException("Expected the repository to have been cloned.");

        Assert.That(created.Location, Is.EqualTo($"/api/workspaces/{workspace.Id}"));
        Assert.That(cloned.Branch, Is.EqualTo("main"));
    }

    private static SourceClonesController CreateController(FakeSourceCloneGitProvider git)
    {
        var identity = new FakeWorkspaceIdentityProvider(new WorkspaceIdentity("/clones/widgets", "main", "abc123"));
        var service = new SourceCloneService(
            new FakeSourceCloneTargetProvider(),
            git,
            new FakeSourceCloneRootProvider("/clones"),
            identity,
            new OrchestrationRegistrationController(new OrchestrationRegistrationService(
                new FakeAgentUpConfigurationProvider(),
                identity)),
            new WorkspaceQueryController(ServerTestComposition.CreateRegistry()));

        return new SourceClonesController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}

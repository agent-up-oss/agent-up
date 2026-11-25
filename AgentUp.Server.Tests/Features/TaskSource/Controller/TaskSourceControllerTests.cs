using AgentUp.Sdk.Forge;
using AgentUp.Server.Features.TaskSource.Controllers;
using AgentUp.Server.Features.TaskSource.DTOs;
using AgentUp.Server.Features.TaskSource.Services;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.TaskSource.Controller;

/// <summary>The controller is a pass-through: what a caller asks for reaches the service.</summary>
[TestFixture]
public sealed class TaskSourceControllerTests
{
    [Test]
    public async Task SaveAndClearReachTheService()
    {
        var controller = Controller();

        var saved = await controller.SaveAsync(new SaveTaskSourceTokenRequest("github", "https://github.com", "token"), CancellationToken.None);
        var cleared = controller.Clear("github");

        Assert.Multiple(() =>
        {
            Assert.That(saved.StatusCode, Is.EqualTo(200));
            Assert.That(((TaskSourceDto)saved.Body!).Modules.Single(module => module.ModuleId == "github").Connected, Is.True);
            Assert.That(cleared.Modules.Single(module => module.ModuleId == "github").Connected, Is.False);
        });
    }

    [Test]
    public async Task IssuesComeBackEmptyWhileNoTokenIsStored()
    {
        var issues = await Controller().ListIssuesAsync("github", "acme", "widgets", CancellationToken.None);

        Assert.That(issues, Is.Empty);
    }

    private static TaskSourceController Controller()
    {
        var packages = new FakeEnabledCapabilityPackages();
        packages.WithForges(new StubForgeCapability());
        return new TaskSourceController(new TaskSourceService(
            new MemoryTaskSourceCredentialStore(),
            packages,
            new EmptyTaskSourceTeam()));
    }
}

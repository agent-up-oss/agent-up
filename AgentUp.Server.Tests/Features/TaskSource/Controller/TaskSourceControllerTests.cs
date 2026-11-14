using AgentUp.Server.Features.Forge.Models;
using AgentUp.Server.Features.TaskSource.Controllers;
using AgentUp.Server.Features.TaskSource.DTOs;
using AgentUp.Server.Features.TaskSource.Providers;
using AgentUp.Server.Features.TaskSource.Services;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.TaskSource.Controller;

/// <summary>
/// The controller is a pass-through, so these cover the boundary rather than the behaviour: that
/// what a caller asks for reaches the service and the answer comes back unchanged.
/// </summary>
[TestFixture]
public sealed class TaskSourceControllerTests
{
    [Test]
    public void DescribeAnswersWhatTheServiceHolds()
    {
        var controller = Controller();

        controller.Set(new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"));

        Assert.Multiple(() =>
        {
            Assert.That(controller.Describe().RepositoryOf(), Is.EqualTo("agent-up-oss/agent-up"));
            Assert.That(controller.Describe().Configured, Is.True);
        });
    }

    [Test]
    public void ClearTakesTheSourceBackOut()
    {
        var controller = Controller();
        controller.Set(new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"));

        Assert.That(controller.Clear().Configured, Is.False);
    }

    [Test]
    public async Task IssuesComeBackEmptyWhileNobodyHasSignedIn()
    {
        var controller = Controller();
        controller.Set(new SetTaskSourceRequest("github", "agent-up-oss", "agent-up"));

        Assert.That(await controller.ListIssuesAsync(CancellationToken.None), Is.Empty);
    }

    [Test]
    public void StartLoginCarriesTheServicesAnswerIncludingItsStatus()
    {
        var controller = Controller();

        Assert.That(controller.StartLogin().StatusCode, Is.EqualTo(409), "nothing is named yet");
    }

    private static TaskSourceController Controller()
    {
        var packages = new FakeEnabledCapabilityPackages();
        packages.WithForges(new StubForgeCapability());
        return new TaskSourceController(new TaskSourceService(
            new MemoryTaskSourceStore(),
            new MemoryTaskSourceCredentialStore(),
            new TaskSourceStateStore(),
            packages,
            new FixedForgeOAuthSettings(new ForgeOAuthConfiguration("client", "secret", "https://example.test/callback")),
            new FixedForgeOAuthExchange("exchanged")));
    }
}

file static class TaskSourceDtoExtensions
{
    public static string RepositoryOf(this TaskSourceDto dto) => $"{dto.Owner}/{dto.Repository}";
}

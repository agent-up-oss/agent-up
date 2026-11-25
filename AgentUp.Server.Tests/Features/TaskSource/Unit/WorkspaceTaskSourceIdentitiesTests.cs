using AgentUp.Server.Features.TaskSource.Services;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.TaskSource.Unit;

[TestFixture]
public sealed class WorkspaceTaskSourceIdentitiesTests
{
    [Test]
    public async Task List_namesEachRegisteredWorkspace()
    {
        var query = new WorkspaceQueryController(ServerTestComposition.CreateRegistry());
        var created = await query.RegisterAsync(ServerDomain.Workspace().Named("Bob").At("/repo/bob").Build());
        var identities = new WorkspaceTaskSourceIdentities(query);

        var people = identities.List();

        Assert.Multiple(() =>
        {
            Assert.That(people.Single().WorkspaceId, Is.EqualTo(created.Id));
            Assert.That(people.Single().DisplayName, Is.EqualTo("Bob"));
        });
    }
}

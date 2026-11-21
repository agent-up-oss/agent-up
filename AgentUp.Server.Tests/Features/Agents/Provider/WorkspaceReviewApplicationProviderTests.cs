using AgentUp.Server.Features.Agents.Providers;
using AgentUp.Server.Features.Processes.Interfaces;
using AgentUp.Server.Features.Workspaces.Controllers;
using AgentUp.Server.Features.Workspaces.Models;
using AgentUp.Server.Tests.Fake;
using AgentUp.Server.Tests.Support;

namespace AgentUp.Server.Tests.Features.Agents.Provider;

/// <summary>The revision a product reviewer was given, started through the Workspaces boundary.</summary>
[TestFixture]
public sealed class WorkspaceReviewApplicationProviderTests
{
    [Test]
    public async Task RunAsync_startsTheWorkspaceAndAnswersWhereItsHttpApplicationListens()
    {
        var (provider, workspaceId) = await ArrangeAsync(new NullWorkspaceProcessManager());

        var (addresses, error) = await provider.RunAsync(workspaceId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Null);
            Assert.That(addresses.Single(), Does.Match(@"^http://127\.0\.0\.1:\d+/$"));
        });
    }

    [Test]
    public async Task RunAsync_opensTheRevisionInTheReviewersOwnBrowser()
    {
        var browser = new RecordingReviewBrowser();
        var (provider, workspaceId) = await ArrangeAsync(new NullWorkspaceProcessManager(), browser);

        var (addresses, error) = await provider.RunAsync(workspaceId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(error, Is.Null);
            Assert.That(browser.Opened, Is.EqualTo(new[] { (workspaceId, addresses[0]) }));
        });
    }

    [Test]
    public async Task RunAsync_reportsABrowserThatCouldNotOpenTheRevision()
    {
        var (provider, workspaceId) = await ArrangeAsync(new NullWorkspaceProcessManager(), new RecordingReviewBrowser("Navigation timed out."));

        var (addresses, error) = await provider.RunAsync(workspaceId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(addresses, Is.Empty);
            Assert.That(error, Does.EndWith(": Navigation timed out."));
        });
    }

    [Test]
    public async Task RunAsync_reportsWhyTheRevisionDidNotStart()
    {
        var (provider, workspaceId) = await ArrangeAsync(new AllFailWorkspaceProcessManager());

        var (addresses, error) = await provider.RunAsync(workspaceId, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(addresses, Is.Empty);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public async Task RunAsync_refusesAWorkspaceThatIsNotRegistered()
    {
        var (provider, _) = await ArrangeAsync(new NullWorkspaceProcessManager());

        var (_, error) = await provider.RunAsync("nobody", CancellationToken.None);

        Assert.That(error, Is.EqualTo("The workspace is not registered."));
    }

    private static async Task<(WorkspaceReviewApplicationProvider Provider, string WorkspaceId)> ArrangeAsync(
        IWorkspaceProcessManager processes,
        RecordingReviewBrowser? browser = null)
    {
        var registry = ServerTestComposition.CreateRegistry();
        var workspace = await registry.RegisterAsync(ServerDomain.Workspace()
            .WithApplication(new ApplicationDefinitionBuilder(ServerDomain.WebName, ServerDomain.WebCommand).WithPort(5700).Build())
            .Build());
        var provider = new WorkspaceReviewApplicationProvider(
            new WorkspaceLifecycleController(ServerTestComposition.CreateWorkspaceLifecycleService(
                registry,
                processes,
                () => true,
                new OpenReadinessProbe(),
                ApplicationReadinessOptions.Immediate)),
            new WorkspaceQueryController(registry),
            browser,
            TimeSpan.FromSeconds(2));
        return (provider, workspace.Id);
    }
}

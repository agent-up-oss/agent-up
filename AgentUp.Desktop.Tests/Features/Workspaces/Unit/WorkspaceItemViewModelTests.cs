using System.Reflection;
using System.Reactive.Linq;
using AgentUp.Desktop.Features.Applications.DTOs;
using AgentUp.Desktop.Features.Ports.DTOs;
using AgentUp.Desktop.Features.Workspaces.DTOs;
using AgentUp.Desktop.Features.Workspaces.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public class WorkspaceItemViewModelTests
{
    private static WorkspaceItemViewModel Build(string displayName) =>
        new("id", displayName, "main", "/repo", "/worktree", "Stopped");

    [Test]
    [TestCase("feat/user-auth", "FA")]  // splits on '-': ["feat/user","auth"] → F+A
    [TestCase("Project Alpha", "PA")]
    [TestCase("agent-1", "A1")]         // splits on '-': ["agent","1"] → A+1
    [TestCase("myapp", "MY")]
    [TestCase("X", "X")]
    public void Initials_derivedFromDisplayName(string displayName, string expected)
    {
        var vm = Build(displayName);
        Assert.That(vm.Initials, Is.EqualTo(expected));
    }

    [Test]
    [TestCase("Running", "#22c55e")]
    [TestCase("Degraded", "#e0a128")]
    [TestCase("Failed", "#d84f4f")]
    [TestCase("Stopped", "#8a9a92")]
    [TestCase("Starting", "#8a9a92")]
    [TestCase("Stopping", "#8a9a92")]
    [TestCase("Unknown", "#8a9a92")]
    public void StateColor_reflectsWorkspaceState(string state, string expectedColor)
    {
        var vm = new WorkspaceItemViewModel("id", "App", "main", "/repo", "/worktree", state);
        Assert.That(vm.StateColor, Is.EqualTo(expectedColor));
    }

    [Test]
    [TestCase("Stopped", true, false, false)]
    [TestCase("Failed", true, false, false)]
    [TestCase("Stopping", true, false, true)]
    [TestCase("Running", false, true, false)]
    [TestCase("Degraded", false, true, false)]
    [TestCase("Starting", false, true, true)]
    public void LifecycleButtons_reflectWorkspaceState(
        string state,
        bool showStart,
        bool showStop,
        bool isBusy)
    {
        var vm = new WorkspaceItemViewModel("id", "App", "main", "/repo", "/worktree", state);

        Assert.Multiple(() =>
        {
            Assert.That(vm.ShowStartButton, Is.EqualTo(showStart));
            Assert.That(vm.ShowStopButton, Is.EqualTo(showStop));
            Assert.That(vm.IsLifecycleBusy, Is.EqualTo(isBusy));
        });
    }

    [Test]
    public void Properties_matchConstructorArguments()
    {
        var vm = new WorkspaceItemViewModel("ws-1", "My Workspace", "feat/foo", "/repo", "/worktree", "Running");

        Assert.Multiple(() =>
        {
            Assert.That(vm.Id, Is.EqualTo("ws-1"));
            Assert.That(vm.DisplayName, Is.EqualTo("My Workspace"));
            Assert.That(vm.Branch, Is.EqualTo("feat/foo"));
            Assert.That(vm.RepositoryPath, Is.EqualTo("/repo"));
            Assert.That(vm.RepositoryName, Is.EqualTo("repo"));
            Assert.That(vm.RepositoryToolTip, Is.EqualTo("/repo"));
            Assert.That(vm.WorktreePath, Is.EqualTo("/worktree"));
            Assert.That(vm.State, Is.EqualTo("Running"));
        });
    }

    [Test]
    public void Subtitle_usesTheRequestedRevisionForADerivedWorkspace()
    {
        var vm = new WorkspaceItemViewModel(
            "ws-1", "Review · topic", "HEAD", "/repo", "/worktree", "Stopped",
            derived: true, sourceRevision: "topic");

        Assert.That(vm.Derived, Is.True);
        Assert.That(vm.Subtitle, Is.EqualTo("topic"));
        Assert.That(vm.ShowRefreshButton, Is.True);
    }

    [Test]
    public async Task RequestDeleteCommand_isDisabledWhenDeleteIsUnavailable()
    {
        var vm = Build("App");
        vm.CanDelete = false;

        Assert.That(vm.ShowDeleteButton, Is.False);
        Assert.That(await vm.RequestDeleteCommand.CanExecute.FirstAsync(), Is.False);
    }

    [Test]
    public async Task RequestDeleteCommand_asksTheHostWhenDeleteIsAvailable()
    {
        var vm = Build("App");
        var host = new RecordingHost();
        vm.SetHost(host);

        await vm.RequestDeleteCommand.Execute().FirstAsync();

        Assert.That(host.Delete, Is.EqualTo(("id", "App")));
    }

    [Test]
    public async Task RequestDeleteCommand_doesNothingWhenNoHostIsAttached()
    {
        var vm = Build("App");

        await vm.RequestDeleteCommand.Execute().FirstAsync();

        Assert.That(vm.ShowDeleteButton, Is.True);
    }

    [Test]
    public void RequestDelete_doesNotAskTheHostWhenDeleteIsUnavailable()
    {
        var vm = Build("App");
        var host = new RecordingHost();
        vm.SetHost(host);
        vm.CanDelete = false;

        InvokeRequestDelete(vm);

        Assert.That(host.Delete, Is.Null);
    }

    [Test]
    public async Task RefreshCommand_asksTheHostToRetargetTheReviewRevision()
    {
        var vm = new WorkspaceItemViewModel(
            "ws-1", "Review · topic", "HEAD", "/repo", "/worktree", "Stopped",
            derived: true, sourceRevision: "topic");
        var host = new RecordingHost();
        vm.SetHost(host);

        await vm.RefreshCommand.Execute().FirstAsync();

        Assert.That(host.Refresh, Is.EqualTo(("ws-1", "topic")));
    }

    [Test]
    public async Task RefreshCommand_doesNothingWhenNoHostIsAttached()
    {
        var vm = new WorkspaceItemViewModel(
            "ws-1", "Review · topic", "HEAD", "/repo", "/worktree", "Stopped",
            derived: true, sourceRevision: "topic");

        await vm.RefreshCommand.Execute().FirstAsync();

        Assert.That(vm.Subtitle, Is.EqualTo("topic"));
    }

    [Test]
    [TestCase("/home/dev/project-a", "project-a")]
    [TestCase("/home/dev/project-a/", "project-a")]
    [TestCase("C:\\repos\\project-b", "project-b")]
    public void RepositoryName_usesLastRepositoryPathSegment(string repositoryPath, string expectedName)
    {
        var vm = new WorkspaceItemViewModel("ws-1", "Fallback", "main", repositoryPath, "/worktree", "Running");

        Assert.That(vm.RepositoryName, Is.EqualTo(expectedName));
        Assert.That(vm.RepositoryToolTip, Is.EqualTo(repositoryPath));
    }

    [Test]
    public void UpdateFrom_AddsUpdatesAndRemovesApplications()
    {
        var vm = new WorkspaceItemViewModel(
            "ws-1",
            "App",
            "main",
            "/repo",
            "/worktree",
            "Stopped",
            [
                new ApplicationDtoBuilder("Old", "old").Build(),
                new ApplicationDtoBuilder("Web", "web").InState("Starting").Build()
            ]);
        var ports = new List<PortMappingDto> { DesktopDomain.Port().Named("WEB_PORT").Declaring(3000).AllocatedTo(5300).Build() };

        vm.UpdateFrom("Running",
            [
                new ApplicationDtoBuilder("Web", "web").WithPorts(ports).Build(),
                new ApplicationDtoBuilder("Worker", "worker").InState("Stopped").Build()
            ]);

        Assert.Multiple(() =>
        {
            Assert.That(vm.State, Is.EqualTo("Running"));
            Assert.That(vm.Applications.Select(app => app.Name), Is.EqualTo(["Web", "Worker"]));
            Assert.That(vm.Applications.Single(app => app.Name == "Web").State, Is.EqualTo("Running"));
            Assert.That(vm.Applications.Single(app => app.Name == "Web").Command, Is.EqualTo("web"));
            Assert.That(vm.Applications.Single(app => app.Name == "Web").AllocatedPorts.Single().AllocatedPort, Is.EqualTo(5300));
            Assert.That(vm.Applications.Single(app => app.Name == "Worker").Command, Is.EqualTo("worker"));
        });
    }

    [Test]
    public void UpdateFrom_RefreshesExistingApplicationMetadata()
    {
        var vm = new WorkspaceItemViewModel(
            "ws-1",
            "App",
            "main",
            "/repo",
            "/worktree",
            "Stopped",
            [new ApplicationDtoBuilder("Web", "old").InState("Starting").Build()]);
        var ports = new List<PortMappingDto> { DesktopDomain.Port().Named("WEB_PORT").Declaring(3000).AllocatedTo(5400).Build() };

        vm.UpdateFrom("Running", [new ApplicationDtoBuilder("Web", "new").WithPorts(ports).Build()]);

        var web = vm.Applications.Single();
        Assert.Multiple(() =>
        {
            Assert.That(web.Command, Is.EqualTo("new"));
            Assert.That(web.AllocatedPorts.Single().AllocatedPort, Is.EqualTo(5400));
            Assert.That(web.State, Is.EqualTo("Running"));
        });
    }

    [Test]
    public void UpdateFrom_RaisesApplicationsChanged_WhenDatabaseFlagChanges()
    {
        var vm = new WorkspaceItemViewModel(
            "ws-1",
            "App",
            "main",
            "/repo",
            "/worktree",
            "Stopped",
            [new ApplicationDtoBuilder("Database", "postgres").Build()]);
        var applicationChangeEvents = 0;
        vm.ApplicationsChanged += (_, _) => applicationChangeEvents++;

        vm.UpdateFrom("Running", [new ApplicationDtoBuilder("Database", "postgres").AsDatabase().Build()]);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Applications.Single().Database, Is.True);
            Assert.That(applicationChangeEvents, Is.EqualTo(1));
        });
    }

    [Test]
    public void UpdateFrom_TreatsNullAllocatedPortsAsEmptyForNewApplication()
    {
        var vm = new WorkspaceItemViewModel("ws-1", "App", "main", "/repo", "/worktree", "Stopped");
        var applicationChangeEvents = 0;
        vm.ApplicationsChanged += (_, _) => applicationChangeEvents++;

        vm.UpdateFrom("Running", [new ApplicationDtoBuilder("Worker", "worker").WithPorts(null!).Build()]);

        Assert.That(vm.Applications.Single().AllocatedPorts, Is.Empty);
        Assert.That(applicationChangeEvents, Is.EqualTo(1));
    }

    [Test]
    public void ApplyStateChange_RaisesApplicationsChanged_whenExistingApplicationStateChanges()
    {
        var vm = new WorkspaceItemViewModel(
            "ws-1",
            "App",
            "main",
            "/repo",
            "/worktree",
            "Stopped",
            [new ApplicationDtoBuilder("Worker", "worker").InState("Starting").Build()]);
        var applicationChangeEvents = 0;
        vm.ApplicationsChanged += (_, _) => applicationChangeEvents++;

        vm.ApplyStateChange("Running", [new AppStateChangeDto("Worker", "Running")]);

        Assert.Multiple(() =>
        {
            Assert.That(vm.Applications.Single().State, Is.EqualTo("Running"));
            Assert.That(applicationChangeEvents, Is.EqualTo(1));
        });
    }

    [Test]
    public void ApplyStateChange_AddsStateOnlyApplicationFromEvent()
    {
        var vm = new WorkspaceItemViewModel("ws-1", "App", "main", "/repo", "/worktree", "Stopped");

        vm.ApplyStateChange("Running", [new AppStateChangeDto("Worker", "Running")]);

        Assert.Multiple(() =>
        {
            Assert.That(vm.State, Is.EqualTo("Running"));
            Assert.That(vm.Applications, Has.Count.EqualTo(1));
            Assert.That(vm.Applications[0].Name, Is.EqualTo("Worker"));
            Assert.That(vm.Applications[0].Command, Is.Empty);
            Assert.That(vm.Applications[0].State, Is.EqualTo("Running"));
        });
    }

    private static void InvokeRequestDelete(WorkspaceItemViewModel vm)
    {
        var method = typeof(WorkspaceItemViewModel).GetMethod(
            "RequestDelete",
            BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("RequestDelete is missing.");
        method.Invoke(vm, null);
    }

    private sealed class RecordingHost : IWorkspaceItemHost
    {
        public (string WorkspaceId, string Revision)? Refresh { get; private set; }

        public Task StartWorkspaceAsync(string workspaceId) => Task.CompletedTask;

        public Task StopWorkspaceAsync(string workspaceId) => Task.CompletedTask;

        public (string WorkspaceId, string DisplayName)? Delete { get; private set; }

        public void RequestDeleteWorkspace(string workspaceId, string displayName)
            => Delete = (workspaceId, displayName);

        public void ShowRefreshDerivedWorkspace(string workspaceId, string currentRevision)
            => Refresh = (workspaceId, currentRevision);
    }
}

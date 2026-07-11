using AgentUp.Desktop.Features.Workspaces.ViewModels;

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
    public void Properties_matchConstructorArguments()
    {
        var vm = new WorkspaceItemViewModel("ws-1", "My Workspace", "feat/foo", "/repo", "/worktree", "Running");

        Assert.Multiple(() =>
        {
            Assert.That(vm.Id, Is.EqualTo("ws-1"));
            Assert.That(vm.DisplayName, Is.EqualTo("My Workspace"));
            Assert.That(vm.Branch, Is.EqualTo("feat/foo"));
            Assert.That(vm.RepositoryPath, Is.EqualTo("/repo"));
            Assert.That(vm.WorktreePath, Is.EqualTo("/worktree"));
            Assert.That(vm.State, Is.EqualTo("Running"));
        });
    }
}

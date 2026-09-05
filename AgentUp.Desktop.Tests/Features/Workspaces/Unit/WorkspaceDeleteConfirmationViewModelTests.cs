using AgentUp.Desktop.Features.Workspaces.ViewModels;
using System.Reactive.Linq;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public class WorkspaceDeleteConfirmationViewModelTests
{
    [Test]
    public async Task ConfirmCommand_runsOnlyWhenCheckboxChecked()
    {
        string? deletedId = null;
        var vm = new WorkspaceDeleteConfirmationViewModel(
            id =>
            {
                deletedId = id;
                return Task.CompletedTask;
            },
            () => { });

        vm.Show("ws-1", "My App");

        Assert.That(vm.ConfirmChecked, Is.False);

        vm.ConfirmChecked = true;
        await vm.ConfirmCommand.Execute().FirstAsync();

        Assert.That(deletedId, Is.EqualTo("ws-1"));
    }

    [Test]
    public void CancelCommand_invokesCancelHandler()
    {
        var cancelled = false;
        WorkspaceDeleteConfirmationViewModel? vm = null;
        vm = new WorkspaceDeleteConfirmationViewModel(
            _ => Task.CompletedTask,
            () =>
            {
                cancelled = true;
                vm!.Hide();
            });
        vm.Show("ws-1", "My App");

        vm.CancelCommand.Execute().Subscribe();

        Assert.Multiple(() =>
        {
            Assert.That(cancelled, Is.True);
            Assert.That(vm.IsVisible, Is.False);
        });
    }
}

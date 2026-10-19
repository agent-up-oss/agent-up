using System.Reactive.Linq;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceRefreshViewModelTests
{
    [Test]
    public async Task ConfirmCommand_isDisabledUntilARevisionIsEntered()
    {
        var viewModel = new WorkspaceRefreshViewModel((_, _) => Task.FromResult(true));
        viewModel.Show("review", "topic");

        Assert.That(await viewModel.ConfirmCommand.CanExecute.FirstAsync(), Is.True);
        Assert.That(viewModel.ConfirmLabel, Is.EqualTo("Refresh"));

        viewModel.Revision = "   ";

        Assert.That(await viewModel.ConfirmCommand.CanExecute.FirstAsync(), Is.False);
    }

    [Test]
    public async Task ConfirmCommand_trimsTheRevisionAndClosesTheDialogOnSuccess()
    {
        (string WorkspaceId, string Revision)? requested = null;
        var viewModel = new WorkspaceRefreshViewModel((workspaceId, revision) =>
        {
            requested = (workspaceId, revision);
            return Task.FromResult(true);
        });
        viewModel.Show("review", "topic");
        viewModel.Revision = "  release  ";
        viewModel.ErrorMessage = "stale";

        await viewModel.ConfirmCommand.Execute().FirstAsync();

        Assert.That(requested, Is.EqualTo(("review", "release")));
        Assert.That(viewModel.IsVisible, Is.False);
        Assert.That(viewModel.Revision, Is.Empty);
        Assert.That(viewModel.IsBusy, Is.False);
        Assert.That(viewModel.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task ConfirmCommand_keepsTheDialogOpenWhenTheRefreshFails()
    {
        var gate = new TaskCompletionSource<bool>();
        var started = new TaskCompletionSource();
        var viewModel = new WorkspaceRefreshViewModel(async (_, _) =>
        {
            started.TrySetResult();
            return await gate.Task;
        });
        viewModel.Show("review", "topic");

        var completed = new TaskCompletionSource();
        using var subscription = viewModel.ConfirmCommand.Execute().Subscribe(
            _ => completed.TrySetResult(),
            completed.SetException);
        await started.Task;

        Assert.That(viewModel.IsBusy, Is.True);
        Assert.That(viewModel.ConfirmLabel, Is.EqualTo("Refreshing…"));
        Assert.That(await viewModel.ConfirmCommand.CanExecute.FirstAsync(), Is.False);

        gate.SetResult(false);
        await completed.Task;

        Assert.That(viewModel.IsVisible, Is.True);
        Assert.That(viewModel.Revision, Is.EqualTo("topic"));
        Assert.That(viewModel.IsBusy, Is.False);
        Assert.That(viewModel.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task ConfirmCommand_clearsBusyWhenRefreshThrows()
    {
        var viewModel = new WorkspaceRefreshViewModel((_, _) => throw new InvalidOperationException("only derived"));
        viewModel.Show("review", "topic");

        _ = await Assert.ThrowsAsync<ReactiveUI.UnhandledErrorException>(async () =>
            await viewModel.ConfirmCommand.Execute().FirstAsync());

        Assert.That(viewModel.IsBusy, Is.False);
        Assert.That(viewModel.IsVisible, Is.True);
        Assert.That(viewModel.Revision, Is.EqualTo("topic"));
    }

    [Test]
    public async Task CancelCommand_hidesAndClearsTheDialog()
    {
        var viewModel = new WorkspaceRefreshViewModel((_, _) => Task.FromResult(true));
        viewModel.Show("review", "topic");
        viewModel.ErrorMessage = "stale";

        await viewModel.CancelCommand.Execute().FirstAsync();

        Assert.That(viewModel.IsVisible, Is.False);
        Assert.That(viewModel.Revision, Is.Empty);
        Assert.That(viewModel.ErrorMessage, Is.Null);
        Assert.That(viewModel.IsBusy, Is.False);
    }
}

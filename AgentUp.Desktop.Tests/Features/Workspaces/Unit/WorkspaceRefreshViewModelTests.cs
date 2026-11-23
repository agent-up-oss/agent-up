using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceRefreshViewModelTests
{
    private static readonly TimeSpan Bounded = TimeSpan.FromSeconds(10);

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
        // Continuations run asynchronously so the test never resumes inline inside the
        // command's own pipeline, and every wait is bounded: in the full suite this test once
        // stalled until the hang detector killed the test host and took every later result with it.
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var viewModel = new WorkspaceRefreshViewModel(async (_, _) =>
        {
            started.TrySetResult();
            return await gate.Task;
        });
        viewModel.Show("review", "topic");

        var completed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = viewModel.ConfirmCommand.Execute().Subscribe(
            _ => completed.TrySetResult(),
            exception => completed.TrySetException(exception));
        await started.Task.WaitAsync(Bounded);

        Assert.That(viewModel.IsBusy, Is.True);
        Assert.That(viewModel.ConfirmLabel, Is.EqualTo("Refreshing…"));
        Assert.That(await viewModel.ConfirmCommand.CanExecute.FirstAsync().ToTask().WaitAsync(Bounded), Is.False);

        gate.SetResult(false);
        await completed.Task.WaitAsync(Bounded);

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

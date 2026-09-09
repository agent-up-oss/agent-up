using System.Reactive.Linq;
using AgentUp.Desktop.Features.Workspaces.ViewModels;

namespace AgentUp.Desktop.Tests.Features.Workspaces.Unit;

[TestFixture]
public sealed class WorkspaceCloneViewModelTests
{
    [Test]
    public async Task ConfirmCommand_isDisabledUntilARepositoryAndBranchAreEntered()
    {
        var viewModel = new WorkspaceCloneViewModel((_, _) => Task.FromResult(true));
        viewModel.Show();
        viewModel.Repository = string.Empty;

        Assert.That(await viewModel.ConfirmCommand.CanExecute.FirstAsync(), Is.False);

        viewModel.Repository = "https://example.test/acme/widgets.git";

        Assert.That(await viewModel.ConfirmCommand.CanExecute.FirstAsync(), Is.True);

        viewModel.Branch = "   ";

        Assert.That(await viewModel.ConfirmCommand.CanExecute.FirstAsync(), Is.False);
    }

    [Test]
    public void Show_resetsTheFormToItsDefaults()
    {
        var viewModel = new WorkspaceCloneViewModel((_, _) => Task.FromResult(true));
        viewModel.Repository = "https://example.test/acme/old.git";
        viewModel.Branch = "release";
        viewModel.ErrorMessage = "boom";

        viewModel.Show();

        Assert.That(viewModel.IsVisible, Is.True);
        Assert.That(viewModel.Repository, Is.Empty);
        Assert.That(viewModel.Branch, Is.EqualTo("main"));
        Assert.That(viewModel.ErrorMessage, Is.Null);
    }

    [Test]
    public async Task ConfirmCommand_trimsInputAndClosesTheDialogOnSuccess()
    {
        (string Repository, string Branch)? requested = null;
        var viewModel = new WorkspaceCloneViewModel((repository, branch) =>
        {
            requested = (repository, branch);
            return Task.FromResult(true);
        });
        viewModel.Show();
        viewModel.Repository = "  https://example.test/acme/widgets.git  ";
        viewModel.Branch = " feature/login ";

        await viewModel.ConfirmCommand.Execute().FirstAsync();

        Assert.That(requested, Is.EqualTo(("https://example.test/acme/widgets.git", "feature/login")));
        Assert.That(viewModel.IsVisible, Is.False);
    }

    [Test]
    public async Task ConfirmCommand_keepsTheDialogOpenWhenTheCloneFails()
    {
        var viewModel = new WorkspaceCloneViewModel((_, _) => Task.FromResult(false));
        viewModel.Show();
        viewModel.Repository = "https://example.test/acme/widgets.git";

        await viewModel.ConfirmCommand.Execute().FirstAsync();

        Assert.That(viewModel.IsVisible, Is.True);
        Assert.That(viewModel.Repository, Is.EqualTo("https://example.test/acme/widgets.git"));
        Assert.That(viewModel.IsBusy, Is.False);
    }

    [Test]
    public async Task CancelCommand_hidesAndClearsTheDialog()
    {
        var viewModel = new WorkspaceCloneViewModel((_, _) => Task.FromResult(true));
        viewModel.Show();
        viewModel.Repository = "https://example.test/acme/widgets.git";

        await viewModel.CancelCommand.Execute().FirstAsync();

        Assert.That(viewModel.IsVisible, Is.False);
        Assert.That(viewModel.Repository, Is.Empty);
    }
}

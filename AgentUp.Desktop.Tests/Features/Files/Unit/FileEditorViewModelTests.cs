using System.Reactive.Linq;
using AgentUp.Desktop.Features.Files.Controllers;
using AgentUp.Desktop.Features.Files.DTOs;
using AgentUp.Desktop.Features.Files.Services;
using AgentUp.Desktop.Features.Files.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Files.Unit;

[TestFixture]
public sealed class FileEditorViewModelTests
{
    [Test]
    public async Task Opening_shows_the_whole_file_with_its_status_and_line_numbers()
    {
        var editor = await OpenedAsync(Api().File(DesktopDomain.FileProductGrid, "one\ntwo\nthree\n", "Modified"), DesktopDomain.FileProductGrid);

        Assert.That(editor.Text, Is.EqualTo("one\ntwo\nthree\n"));
        Assert.That(editor.StatusLabel, Is.EqualTo("Modified"));
        Assert.That(editor.LineNumbers, Is.EqualTo("1\n2\n3\n4"));
        Assert.That(editor.StateLabel, Is.EqualTo("Saved"));
        Assert.That(editor.FileName, Is.EqualTo("ProductGrid.tsx"));
    }

    [Test]
    public async Task Typing_marks_the_page_unsaved_and_Save_writes_from_the_opened_version()
    {
        var api = Api().File(DesktopDomain.FileReadme, "# My App\n");
        var editor = await OpenedAsync(api, DesktopDomain.FileReadme);

        editor.Text = "# My App\n\nEdited.\n";
        var dirty = editor.StateLabel;
        await editor.SaveCommand.Execute();

        Assert.That(dirty, Is.EqualTo("Unsaved changes"));
        Assert.That(api.Saved.Single().Version, Is.EqualTo("v-# My App\n"));
        Assert.That(editor.IsDirty, Is.False);
        Assert.That(editor.StatusLabel, Is.EqualTo("Modified"));
    }

    [Test]
    public async Task Save_is_unavailable_until_something_changed()
    {
        var editor = await OpenedAsync(Api().File(DesktopDomain.FileReadme, "# My App\n"), DesktopDomain.FileReadme);

        Assert.That(await editor.SaveCommand.CanExecute.FirstAsync(), Is.False);
    }

    [Test]
    public async Task A_stale_save_keeps_the_edit_and_offers_reload()
    {
        var api = Api().File(DesktopDomain.FileReadme, "# My App\n");
        var editor = await OpenedAsync(api, DesktopDomain.FileReadme);
        api.NextSave = WorkspaceFileSaveResultDto.Failed("'README.md' changed on the Server since it was opened.", conflict: true);

        editor.Text = "mine";
        await editor.SaveCommand.Execute();

        Assert.That(editor.IsConflict, Is.True);
        Assert.That(editor.Text, Is.EqualTo("mine"));
        Assert.That(editor.StateLabel, Does.Contain("changed on the Server"));
        Assert.That(await editor.SaveCommand.CanExecute.FirstAsync(), Is.False);
    }

    [Test]
    public async Task Reload_after_a_conflict_shows_what_is_on_the_Server_now()
    {
        var api = Api().File(DesktopDomain.FileReadme, "# My App\n");
        var editor = await OpenedAsync(api, DesktopDomain.FileReadme);
        api.NextSave = WorkspaceFileSaveResultDto.Failed("changed", conflict: true);
        editor.Text = "mine";
        await editor.SaveCommand.Execute();
        api.File(DesktopDomain.FileReadme, "# The agent's edit\n");

        await editor.ReloadCommand.Execute();

        Assert.That(editor.Text, Is.EqualTo("# The agent's edit\n"));
        Assert.That(editor.IsConflict, Is.False);
    }

    [Test]
    public async Task Revert_returns_to_the_text_that_was_opened()
    {
        var editor = await OpenedAsync(Api().File(DesktopDomain.FileReadme, "# My App\n"), DesktopDomain.FileReadme);
        editor.Text = "changed";

        await editor.RevertCommand.Execute();

        Assert.That(editor.Text, Is.EqualTo("# My App\n"));
        Assert.That(editor.IsDirty, Is.False);
    }

    [Test]
    public async Task Back_with_unsaved_changes_asks_before_leaving()
    {
        var editor = await OpenedAsync(Api().File(DesktopDomain.FileReadme, "# My App\n"), DesktopDomain.FileReadme);
        editor.Text = "changed";

        await editor.BackCommand.Execute();
        var asked = editor.IsConfirmingDiscard;
        await editor.KeepEditingCommand.Execute();

        Assert.That(asked, Is.True);
        Assert.That(editor.IsOpen, Is.True);
        Assert.That(editor.Text, Is.EqualTo("changed"));
    }

    [Test]
    public async Task Discard_closes_the_page()
    {
        var editor = await OpenedAsync(Api().File(DesktopDomain.FileReadme, "# My App\n"), DesktopDomain.FileReadme);
        editor.Text = "changed";
        await editor.BackCommand.Execute();

        await editor.DiscardCommand.Execute();

        Assert.That(editor.IsOpen, Is.False);
    }

    [Test]
    public async Task Back_without_changes_closes_at_once()
    {
        var editor = await OpenedAsync(Api().File(DesktopDomain.FileReadme, "# My App\n"), DesktopDomain.FileReadme);

        await editor.BackCommand.Execute();

        Assert.That(editor.IsOpen, Is.False);
    }

    [Test]
    public async Task A_binary_file_says_why_it_has_no_editor()
    {
        var editor = await OpenedAsync(Api().File("logo.png", null, isBinary: true), "logo.png");

        Assert.That(editor.HasContent, Is.False);
        Assert.That(editor.Notice, Does.Contain("binary"));
        Assert.That(editor.IsReadOnly, Is.True);
    }

    [Test]
    public async Task Without_Git_write_permission_the_page_is_read_only()
    {
        var editor = await OpenedAsync(Api().File(DesktopDomain.FileReadme, "# My App\n"), DesktopDomain.FileReadme);

        editor.CanWriteGit = false;

        Assert.That(editor.IsReadOnly, Is.True);
        Assert.That(editor.StateLabel, Does.StartWith("Read only"));
    }

    [Test]
    public async Task The_caret_reads_as_line_and_column()
    {
        var editor = await OpenedAsync(Api().File(DesktopDomain.FileReadme, "one\ntwo"), DesktopDomain.FileReadme);

        editor.CaretIndex = 6;

        Assert.That(editor.CaretLabel, Is.EqualTo("Ln 2, Col 3"));
    }

    [Test]
    public async Task A_file_that_is_gone_says_so()
    {
        var editor = await OpenedAsync(Api(), "gone.md");

        Assert.That(editor.Notice, Does.Contain("no longer in the workspace"));
    }

    private static FakeWorkspaceFilesApi Api() => new();

    private static async Task<FileEditorViewModel> OpenedAsync(FakeWorkspaceFilesApi api, string path)
    {
        var editor = new FileEditorViewModel(new WorkspaceFilesController(new WorkspaceFilesService(api)));
        await editor.OpenAsync(DesktopDomain.WorkspaceId, path);
        return editor;
    }
}

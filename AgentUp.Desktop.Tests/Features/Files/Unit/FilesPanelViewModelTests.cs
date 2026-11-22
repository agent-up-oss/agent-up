using System.Reactive.Linq;
using AgentUp.Desktop.Features.Files.Controllers;
using AgentUp.Desktop.Features.Files.Services;
using AgentUp.Desktop.Features.Files.ViewModels;
using AgentUp.Desktop.Tests.Support;

namespace AgentUp.Desktop.Tests.Features.Files.Unit;

[TestFixture]
public sealed class FilesPanelViewModelTests
{
    [Test]
    public async Task Showing_the_tab_reads_only_the_top_level()
    {
        var api = Repository();
        var panel = Panel(api);
        panel.PrepareWorkspace(DesktopDomain.WorkspaceId, "My App · main", "local");

        panel.IsVisible = true;
        await WaitAsync(() => !panel.IsLoading);

        Assert.That(panel.Nodes.Select(node => node.Name), Is.EqualTo(new[] { "apps", "README.md" }));
        Assert.That(api.Requested, Is.EqualTo(new[] { "" }));
    }

    [Test]
    public async Task Nothing_is_read_while_the_tab_is_hidden()
    {
        var api = Repository();
        var panel = Panel(api);

        panel.PrepareWorkspace(DesktopDomain.WorkspaceId, "My App · main", "local");
        await Task.Yield();

        Assert.That(api.Requested, Is.Empty);
    }

    [Test]
    public async Task Unfolding_a_folder_reads_it_and_puts_its_rows_under_it()
    {
        var api = Repository();
        var panel = await ShownAsync(api);

        await panel.Nodes[0].ToggleCommand.Execute();

        Assert.That(panel.Nodes.Select(node => node.Path), Is.EqualTo(new[] { "apps", "apps/storefront", "apps/package.json", "README.md" }));
        Assert.That(panel.Nodes[1].Depth, Is.EqualTo(1));
        Assert.That(api.Requested, Is.EqualTo(new[] { "", "apps" }));
    }

    [Test]
    public async Task Clicking_a_folder_name_unfolds_it_like_its_chevron()
    {
        var panel = await ShownAsync(Repository());

        await panel.Nodes[0].OpenCommand.Execute();

        Assert.That(panel.Nodes[0].IsExpanded, Is.True);
        Assert.That(panel.IsEditorOpen, Is.False);
    }

    [Test]
    public async Task Folding_hides_every_row_under_the_folder_and_unfolding_again_asks_nothing()
    {
        var api = Repository();
        var panel = await ShownAsync(api);
        await panel.Nodes[0].ToggleCommand.Execute();
        await panel.Nodes[1].ToggleCommand.Execute();

        await panel.Nodes[0].ToggleCommand.Execute();
        await panel.Nodes[0].ToggleCommand.Execute();

        Assert.That(panel.Nodes.Select(node => node.Path), Is.EqualTo(new[] { "apps", "apps/storefront", DesktopDomain.FileProductGrid, "apps/package.json", "README.md" }));
        Assert.That(api.Requested, Is.EqualTo(new[] { "", "apps", "apps/storefront" }));
    }

    [Test]
    public async Task Rows_carry_the_git_glyph_and_folder_change_counts()
    {
        var panel = await ShownAsync(Repository());
        await panel.Nodes[0].ToggleCommand.Execute();
        await panel.Nodes[1].ToggleCommand.Execute();

        var apps = panel.Nodes[0];
        var grid = panel.Nodes.Single(node => node.Path == DesktopDomain.FileProductGrid);
        Assert.That(apps.HasChanges, Is.True);
        Assert.That(apps.ChangedLabel, Is.EqualTo("1"));
        Assert.That(grid.StatusGlyph, Is.EqualTo("M"));
        Assert.That(grid.IsModified, Is.True);
        Assert.That(panel.Nodes.Single(node => node.Path == "apps/package.json").HasStatus, Is.False);
    }

    [Test]
    public async Task Reload_reads_the_folders_that_are_still_open_and_keeps_them_open()
    {
        var api = Repository();
        var panel = await ShownAsync(api);
        await panel.Nodes[0].ToggleCommand.Execute();
        api.Requested.Clear();

        await panel.RefreshCommand.Execute();

        Assert.That(api.Requested, Is.EqualTo(new[] { "", "apps" }));
        Assert.That(panel.Nodes.Select(node => node.Path), Does.Contain("apps/storefront"));
    }

    [Test]
    public async Task Opening_a_file_shows_its_page_in_place_of_the_tree()
    {
        var panel = await ShownAsync(Repository());

        await panel.Nodes[1].OpenCommand.Execute();

        Assert.That(panel.IsEditorOpen, Is.True);
        Assert.That(panel.IsTreeVisible, Is.False);
        Assert.That(panel.Nodes[1].IsOpen, Is.True);
        Assert.That(panel.Editor.Text, Is.EqualTo("# My App\n"));
    }

    [Test]
    public async Task Another_workspace_starts_the_tree_over_and_closes_the_page()
    {
        var api = Repository();
        var panel = await ShownAsync(api);
        await panel.Nodes[1].OpenCommand.Execute();

        panel.PrepareWorkspace(DesktopDomain.SecondWorkspaceId, "Auth Service · main", "local");
        await WaitAsync(() => !panel.IsLoading);

        Assert.That(panel.IsEditorOpen, Is.False);
        Assert.That(api.Requested, Is.EqualTo(new[] { "", "" }));
    }

    [Test]
    public async Task A_folder_the_Server_cut_short_ends_with_a_notice()
    {
        var api = new FakeWorkspaceFilesApi();
        api.Folders[""] = new(DesktopDomain.WorkspaceId, "", [FakeWorkspaceFilesApi.Leaf("a.txt")], true);

        var panel = await ShownAsync(api);

        Assert.That(panel.Nodes.Last().IsNotice, Is.True);
        Assert.That(panel.Nodes.Last().Name, Does.Contain("first 1 entries"));
    }

    [Test]
    public async Task A_workspace_the_Server_no_longer_has_says_so()
    {
        var panel = await ShownAsync(new FakeWorkspaceFilesApi());

        Assert.That(panel.ErrorMessage, Does.Contain("no longer on the Server"));
        Assert.That(panel.ShowEmptyState, Is.False);
    }

    internal static FakeWorkspaceFilesApi Repository() => new FakeWorkspaceFilesApi()
        .Folder("", FakeWorkspaceFilesApi.Dir("apps", changed: 1), FakeWorkspaceFilesApi.Leaf("README.md"))
        .Folder("apps", FakeWorkspaceFilesApi.Dir("apps/storefront", changed: 1), FakeWorkspaceFilesApi.Leaf("apps/package.json"))
        .Folder("apps/storefront", FakeWorkspaceFilesApi.Leaf(DesktopDomain.FileProductGrid, "Modified"))
        .File("README.md", "# My App\n")
        .File(DesktopDomain.FileProductGrid, "export function ProductGrid() {}\n", "Modified");

    private static FilesPanelViewModel Panel(FakeWorkspaceFilesApi api) => new(new WorkspaceFilesController(new WorkspaceFilesService(api)));

    private static async Task<FilesPanelViewModel> ShownAsync(FakeWorkspaceFilesApi api)
    {
        var panel = Panel(api);
        panel.PrepareWorkspace(DesktopDomain.WorkspaceId, "My App · main", "local");
        panel.IsVisible = true;
        await WaitAsync(() => !panel.IsLoading);
        return panel;
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
            await Task.Delay(10);
    }
}

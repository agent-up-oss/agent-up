using AgentUp.Server.Features.Workspaces.DTOs;
using AgentUp.Server.Features.Workspaces.Repositories;

namespace AgentUp.Server.Tests.Features.Workspaces.Repository;

[TestFixture]
public class WorkspaceRepositoryTests
{
    private string _testDir = null!;
    private JsonWorkspaceRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "AgentUp-Tests", Guid.NewGuid().ToString());
        _repository = new JsonWorkspaceRepository(Path.Combine(_testDir, "workspaces.json"));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, recursive: true);
    }

    [Test]
    public async Task LoadAll_ReturnsEmpty_WhenFileDoesNotExist()
    {
        var workspaces = await _repository.LoadAllAsync();

        Assert.That(workspaces, Is.Empty);
    }

    [Test]
    public async Task SaveAll_CreatesFile_WithWorkspaces()
    {
        var workspaces = new List<Workspace>
        {
            MakeWorkspace("id-1", "Agent 1", "feature/alpha")
        };

        await _repository.SaveAllAsync(workspaces);

        Assert.That(File.Exists(Path.Combine(_testDir, "workspaces.json")), Is.True);
    }

    [Test]
    public async Task LoadAll_AfterSave_RoundTripsAllFields()
    {
        var original = MakeWorkspace("id-1", "Agent 1", "feature/alpha",
            repositoryPath: "/repos/app",
            worktreePath: "/repos/app/.worktrees/alpha",
            commit: "abc1234",
            state: WorkspaceState.Running);

        await _repository.SaveAllAsync([original]);
        var loaded = await _repository.LoadAllAsync();

        Assert.That(loaded, Has.Count.EqualTo(1));
        var w = loaded[0];
        Assert.That(w.Id, Is.EqualTo("id-1"));
        Assert.That(w.DisplayName, Is.EqualTo("Agent 1"));
        Assert.That(w.Branch, Is.EqualTo("feature/alpha"));
        Assert.That(w.RepositoryPath, Is.EqualTo("/repos/app"));
        Assert.That(w.WorktreePath, Is.EqualTo("/repos/app/.worktrees/alpha"));
        Assert.That(w.Commit, Is.EqualTo("abc1234"));
        Assert.That(w.State, Is.EqualTo(WorkspaceState.Running));
    }

    [Test]
    public async Task LoadAll_AfterSave_RoundTripsMultipleWorkspaces()
    {
        var workspaces = new List<Workspace>
        {
            MakeWorkspace("id-1", "Alpha", "feature/alpha"),
            MakeWorkspace("id-2", "Beta", "feature/beta"),
            MakeWorkspace("id-3", "Gamma", "main")
        };

        await _repository.SaveAllAsync(workspaces);
        var loaded = await _repository.LoadAllAsync();

        Assert.That(loaded, Has.Count.EqualTo(3));
        Assert.That(loaded.Select(w => w.Id), Is.EquivalentTo(new[] { "id-1", "id-2", "id-3" }));
    }

    [Test]
    public async Task SaveAll_Overwrites_PreviouslySavedData()
    {
        await _repository.SaveAllAsync([MakeWorkspace("id-1", "Alpha", "main")]);
        await _repository.SaveAllAsync([MakeWorkspace("id-2", "Beta", "feature/x")]);

        var loaded = await _repository.LoadAllAsync();

        Assert.That(loaded, Has.Count.EqualTo(1));
        Assert.That(loaded[0].Id, Is.EqualTo("id-2"));
    }

    [Test]
    public async Task SaveAll_EmptyList_ClearsPersistedData()
    {
        await _repository.SaveAllAsync([MakeWorkspace("id-1", "Alpha", "main")]);
        await _repository.SaveAllAsync([]);

        var loaded = await _repository.LoadAllAsync();

        Assert.That(loaded, Is.Empty);
    }

    [Test]
    public async Task LoadAll_CreatesDirectory_WhenMissing()
    {
        var nestedDir = Path.Combine(_testDir, "sub", "nested");
        var repository = new JsonWorkspaceRepository(Path.Combine(nestedDir, "workspaces.json"));

        var workspaces = await repository.LoadAllAsync();

        Assert.That(workspaces, Is.Empty);
        Assert.That(Directory.Exists(nestedDir), Is.True);
    }

    [Test]
    public async Task LoadAll_ReturnsEmpty_OnCorruptFile()
    {
        Directory.CreateDirectory(_testDir);
        await File.WriteAllTextAsync(Path.Combine(_testDir, "workspaces.json"), "{ not valid json [[[");

        var loaded = await _repository.LoadAllAsync();

        Assert.That(loaded, Is.Empty);
    }

    [Test]
    public async Task SaveThenLoad_UseTempFile_SoWriteIsAtomic()
    {
        var workspaces = new List<Workspace>
        {
            MakeWorkspace("id-1", "Alpha", "main")
        };

        await _repository.SaveAllAsync(workspaces);

        Assert.That(File.Exists(Path.Combine(_testDir, "workspaces.json.tmp")), Is.False,
            "Temp file should be removed after successful save");
    }

    private static Workspace MakeWorkspace(
        string id,
        string displayName,
        string branch,
        string repositoryPath = "/r",
        string worktreePath = "/r/w",
        string commit = "c1",
        WorkspaceState state = WorkspaceState.Stopped) =>
        new()
        {
            Id = id,
            DisplayName = displayName,
            Branch = branch,
            RepositoryPath = repositoryPath,
            WorktreePath = worktreePath,
            Commit = commit,
            State = state
        };
}

using AgentUp.Server.Features.Files.Providers;
using AgentUp.Server.Features.Files.Services;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Tests.Fake;

/// <summary>Composes the Files slice over fakes. It registers no workspace; each test does that itself.</summary>
internal static class WorkspaceFilesComposition
{
    public static WorkspaceFilesService Service(
        WorkspaceQueryController workspaces,
        FakeWorkspaceFileSystemProvider files,
        FakeWorkspaceFileChangeProvider changes,
        FakeWorkspaceFileEditGuard guard)
        => new(workspaces, files, changes, guard, new WorkspaceFileTextProvider());
}

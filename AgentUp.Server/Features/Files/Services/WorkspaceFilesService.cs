using System.Collections.Concurrent;
using AgentUp.Server.Features.Files.DTOs;
using AgentUp.Server.Features.Files.Interfaces;
using AgentUp.Server.Features.Files.Models;
using AgentUp.Server.Features.Files.Providers;
using AgentUp.Server.Features.Git.DTOs;
using AgentUp.Server.Features.Workspaces.Controllers;

namespace AgentUp.Server.Features.Files.Services;

/// <summary>
/// A workspace's own files, for a person who wants to see what is in the clone rather than only
/// what changed. Folders are read one level at a time as a client unfolds them, files are read
/// whole, and an edit replaces a file only when nobody else has changed it since it was read.
/// </summary>
public sealed class WorkspaceFilesService(
    WorkspaceQueryController workspaces,
    IWorkspaceFileSystemProvider files,
    IWorkspaceFileChangeProvider changes,
    IWorkspaceFileEditGuard guard,
    WorkspaceFileTextProvider text)
{
    private const string AgentWorking = "The workspace agent is working. Edit files once its turn has finished.";
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _writes = new(StringComparer.Ordinal);

    public async Task<WorkspaceFileResult<WorkspaceDirectoryListing>> GetDirectoryAsync(
        string workspaceId,
        string? path,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return WorkspaceFileResult<WorkspaceDirectoryListing>.Failed(WorkspaceFileOutcome.WorkspaceNotFound, "Workspace not found.");

        WorkspaceDiskListing? listing;
        try
        {
            listing = files.ListDirectory(workspace.WorktreePath, path ?? string.Empty, WorkspaceFileLimits.MaxEntries);
        }
        catch (Exception ex) when (ex is WorkspaceFilePathException or IOException or UnauthorizedAccessException)
        {
            return WorkspaceFileResult<WorkspaceDirectoryListing>.Failed(WorkspaceFileOutcome.Invalid, ex.Message);
        }
        if (listing is null)
            return WorkspaceFileResult<WorkspaceDirectoryListing>.Failed(WorkspaceFileOutcome.FileNotFound, $"'{path}' is not a folder in this workspace.");

        var changed = await changes.GetChangesAsync(workspaceId, cancellationToken);
        var statuses = changed.ToDictionary(change => change.Path, change => change.Status, StringComparer.Ordinal);
        var entries = listing.Entries
            .Select(entry => new WorkspaceFileEntry(
                entry.Name,
                entry.Path,
                entry.Kind,
                entry.Size,
                entry.Kind == WorkspaceFileEntryKind.File && statuses.TryGetValue(entry.Path, out var status) ? status : null,
                entry.Kind == WorkspaceFileEntryKind.Directory ? ChangedUnder(changed, entry.Path) : 0))
            .ToList();
        return WorkspaceFileResult<WorkspaceDirectoryListing>.Success(
            new WorkspaceDirectoryListing(workspace.Id, listing.Path, entries, listing.Truncated));
    }

    public async Task<WorkspaceFileResult<WorkspaceFileContent>> GetFileAsync(
        string workspaceId,
        string? path,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return WorkspaceFileResult<WorkspaceFileContent>.Failed(WorkspaceFileOutcome.WorkspaceNotFound, "Workspace not found.");

        var read = await ReadAsync(workspace.WorktreePath, path, cancellationToken);
        if (read.Outcome != WorkspaceFileOutcome.Succeeded)
            return WorkspaceFileResult<WorkspaceFileContent>.Failed(read.Outcome, read.Error!);
        return WorkspaceFileResult<WorkspaceFileContent>.Success(await DescribeAsync(workspaceId, read.Value!, cancellationToken));
    }

    public async Task<WorkspaceFileResult<WorkspaceFileContent>> WriteFileAsync(
        string workspaceId,
        WorkspaceFileWriteRequest? request,
        CancellationToken cancellationToken = default)
    {
        var workspace = workspaces.GetById(workspaceId);
        if (workspace is null)
            return WorkspaceFileResult<WorkspaceFileContent>.Failed(WorkspaceFileOutcome.WorkspaceNotFound, "Workspace not found.");
        if (request?.Content is null || string.IsNullOrEmpty(request.Version))
            return WorkspaceFileResult<WorkspaceFileContent>.Failed(WorkspaceFileOutcome.Invalid, "A save names the file, its new content, and the version it was edited from.");
        if (guard.IsAgentWorking(workspaceId))
            return WorkspaceFileResult<WorkspaceFileContent>.Failed(WorkspaceFileOutcome.Busy, AgentWorking);

        var gate = _writes.GetOrAdd(workspace.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var read = await ReadAsync(workspace.WorktreePath, request.Path, cancellationToken);
            if (read.Outcome != WorkspaceFileOutcome.Succeeded)
                return WorkspaceFileResult<WorkspaceFileContent>.Failed(read.Outcome, read.Error!);
            var current = read.Value!;
            var decoded = current.Bytes is null ? null : text.Decode(current.Bytes);
            if (decoded is null)
                return WorkspaceFileResult<WorkspaceFileContent>.Failed(WorkspaceFileOutcome.Invalid, $"'{current.Path}' is binary or too large to edit here.");
            if (!string.Equals(text.Version(current.Bytes!), request.Version, StringComparison.Ordinal))
                return WorkspaceFileResult<WorkspaceFileContent>.Failed(WorkspaceFileOutcome.Stale, $"'{current.Path}' changed on the Server since it was opened. Reload it to see the change.");

            var bytes = text.Encode(request.Content, decoded.HasBom);
            if (bytes.LongLength > WorkspaceFileLimits.MaxContentBytes)
                return WorkspaceFileResult<WorkspaceFileContent>.Failed(WorkspaceFileOutcome.Invalid, "The edited file is larger than the Server accepts from a client.");
            try
            {
                await files.WriteFileAsync(workspace.WorktreePath, current.Path, bytes, cancellationToken);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return WorkspaceFileResult<WorkspaceFileContent>.Failed(WorkspaceFileOutcome.Invalid, $"'{current.Path}' could not be written: {ex.Message}");
            }
            return WorkspaceFileResult<WorkspaceFileContent>.Success(
                await DescribeAsync(workspaceId, current with { Size = bytes.LongLength, Bytes = bytes }, cancellationToken));
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<WorkspaceFileResult<WorkspaceDiskFile>> ReadAsync(string root, string? path, CancellationToken cancellationToken)
    {
        WorkspaceDiskFile? file;
        try
        {
            file = await files.ReadFileAsync(root, path ?? string.Empty, WorkspaceFileLimits.MaxContentBytes, cancellationToken);
        }
        catch (Exception ex) when (ex is WorkspaceFilePathException or IOException or UnauthorizedAccessException)
        {
            return WorkspaceFileResult<WorkspaceDiskFile>.Failed(WorkspaceFileOutcome.Invalid, ex.Message);
        }
        return file is null
            ? WorkspaceFileResult<WorkspaceDiskFile>.Failed(WorkspaceFileOutcome.FileNotFound, $"'{path}' is not a file in this workspace.")
            : WorkspaceFileResult<WorkspaceDiskFile>.Success(file);
    }

    private async Task<WorkspaceFileContent> DescribeAsync(string workspaceId, WorkspaceDiskFile file, CancellationToken cancellationToken)
    {
        var changed = await changes.GetChangesAsync(workspaceId, cancellationToken);
        GitChangeStatus? status = changed.FirstOrDefault(change => string.Equals(change.Path, file.Path, StringComparison.Ordinal))?.Status;
        if (file.Bytes is null)
            return new WorkspaceFileContent(file.Path, file.Size, text.SizeVersion(file.Size), status, IsBinary: false, IsTooLarge: true, Content: null);
        var decoded = text.Decode(file.Bytes);
        return new WorkspaceFileContent(
            file.Path,
            file.Size,
            text.Version(file.Bytes),
            status,
            IsBinary: decoded is null,
            IsTooLarge: false,
            Content: decoded?.Text);
    }

    /// <summary>Changed files under a folder that are still on disk; a deleted file has nothing left to show.</summary>
    private static int ChangedUnder(IReadOnlyList<GitChangeEntry> changed, string directory)
    {
        var prefix = directory + "/";
        return changed.Count(change => change.Status != GitChangeStatus.Deleted && change.Path.StartsWith(prefix, StringComparison.Ordinal));
    }
}

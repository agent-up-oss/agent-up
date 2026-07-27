using System.Text.Json;
using AgentUp.CLI.Features.Commits.DTOs;
using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Features.Commits.Services;

public sealed class CommitsOutputService(TextWriter output)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public int WriteEnqueueResult(string slice, int totalCount)
    {
        output.WriteLine($"Queued: {slice} ({totalCount} {(totalCount == 1 ? "entry" : "entries")} total)");
        return 0;
    }

    public int WriteStatus(CommitsStatusResult result)
    {
        if (result.Entries.Count == 0)
        {
            output.WriteLine("No commits queued.");
            return 0;
        }

        for (var i = 0; i < result.Entries.Count; i++)
        {
            var entry = result.Entries[i];
            output.WriteLine($"[{i + 1}] {entry.Slice}");
            output.WriteLine($"    {entry.Message}");
            output.WriteLine($"    {entry.Files.Count} file(s){(entry.Tests.Count > 0 ? $", {entry.Tests.Count} test command(s)" : "")}");
        }

        if (result.ActiveSession is not null)
        {
            output.WriteLine();
            output.WriteLine($"Active edit session: {result.ActiveSession.EntryId}");
        }

        if (result.UnassignedFiles.Count > 0)
        {
            output.WriteLine();
            output.WriteLine($"Warning: {result.UnassignedFiles.Count} modified file(s) not in any queued entry:");
            foreach (var file in result.UnassignedFiles)
                output.WriteLine($"  {file}");
        }

        return 0;
    }

    public int WriteStatus(CommitsStatusResult result, CommitsOutputFormat format)
        => format == CommitsOutputFormat.Json ? WriteStatusJson(result) : WriteStatus(result);

    public int WriteStatusJson(CommitsStatusResult result)
    {
        output.WriteLine(JsonSerializer.Serialize(new CommitsStatusJson(
            result.Entries.Count,
            result.Entries.Select(entry => new CommitsStatusEntryJson(entry.Id, entry.Slice, entry.Message, entry.Files, entry.Tests)).ToList(),
            result.UnassignedFiles,
            result.ActiveSession is null ? null : new CommitsStatusSessionJson(result.ActiveSession.EntryId, result.ActiveSession.Files)), JsonOptions));
        return 0;
    }

    public int WriteChanges(CommitChangesResult result, CommitsOutputFormat format)
    {
        if (format == CommitsOutputFormat.Json)
        {
            output.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }

        output.WriteLine("Working tree changes:");
        WriteFiles("Modified", result.ModifiedFiles);
        WriteFiles("Staged", result.StagedFiles);
        WriteFiles("Untracked", result.UntrackedFiles);
        WriteFiles("Queued", result.QueuedFiles);
        WriteFiles("Unassigned", result.UnassignedFiles);
        return 0;
    }

    public int WriteInspect(CommitInspectResult result, CommitsOutputFormat format)
    {
        if (format == CommitsOutputFormat.Json)
        {
            output.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }

        output.WriteLine($"[{result.Entry.Id}] {result.Entry.Slice}");
        output.WriteLine($"  {result.Entry.Message}");
        WriteFiles("Files", result.Entry.Files);
        WriteFiles("Tests", result.Entry.Tests);
        if (result.Patch is not null)
            output.WriteLine(result.Patch);
        return 0;
    }

    public int WriteEdit(CommitEditResult result, CommitsOutputFormat format)
    {
        if (!result.Success)
            return WriteError(result.Message, format);

        if (format == CommitsOutputFormat.Json)
        {
            output.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return 0;
        }

        output.WriteLine(result.Message);
        if (result.Entry is not null)
            output.WriteLine($"{result.Entry.Id} {result.Entry.Message}");
        return 0;
    }

    public int WriteGuard(CommitGuardResult result, CommitsOutputFormat format)
    {
        if (format == CommitsOutputFormat.Json)
        {
            output.WriteLine(JsonSerializer.Serialize(result, JsonOptions));
            return result.Success ? 0 : 1;
        }

        if (result.Success)
        {
            output.WriteLine("Commit queue guard passed.");
            return 0;
        }

        foreach (var blocker in result.Blockers)
            output.WriteLine($"Error: {blocker}");
        return 1;
    }

    public int WriteStagingResult(CommitsStagingResult result)
    {
        if (result.IsBlocked)
            return WriteError(result.BlockedReason!);

        output.WriteLine($"Staged: {result.Slice}");
        foreach (var file in result.StagedFiles)
            output.WriteLine($"  {file}");
        output.WriteLine();
        output.WriteLine($"Suggested commit:");
        output.WriteLine($"  git commit -m \"{result.Message}\"");
        if (result.RemainingCount > 0)
            output.WriteLine($"  ({result.RemainingCount} {(result.RemainingCount == 1 ? "entry" : "entries")} remaining — run 'agentup commits next' after committing)");
        else
            output.WriteLine("  (queue is now empty)");
        return 0;
    }

    public int WriteStagingResult(CommitsStagingResult? result, CommitsOutputFormat format)
    {
        if (format == CommitsOutputFormat.Json)
            return result is null ? WriteEmptyQueueJson() : WriteStagingResultJson(result);

        return result is null ? WriteEmptyQueue("next") : WriteStagingResult(result);
    }

    public int WriteStagingResultJson(CommitsStagingResult result)
    {
        if (result.IsBlocked)
            return WriteErrorJson(result.BlockedReason!);

        output.WriteLine(JsonSerializer.Serialize(new CommitsNextStagedJson(
            true,
            result.Slice,
            result.Message,
            result.RemainingCount), JsonOptions));
        return 0;
    }

    public int WriteEmptyQueueJson()
    {
        output.WriteLine(JsonSerializer.Serialize(new CommitsNextEmptyJson(false, true, null, 0), JsonOptions));
        return 0;
    }

    public int WriteEmptyQueue(string command)
    {
        output.WriteLine($"Queue is empty. Use 'agentup commits enqueue' to add entries.");
        return 0;
    }

    public int WriteCleared()
    {
        output.WriteLine("Queue cleared.");
        return 0;
    }

    public int WriteError(string message)
    {
        output.WriteLine($"Error: {message}");
        return 1;
    }

    public int WriteError(string message, CommitsOutputFormat format)
        => format == CommitsOutputFormat.Json ? WriteErrorJson(message) : WriteError(message);

    public int WriteErrorJson(string message)
    {
        output.WriteLine(JsonSerializer.Serialize(new CommitsErrorJson(message), JsonOptions));
        return 1;
    }

    public int WriteHelp()
    {
        output.WriteLine("Usage: agentup commits <command>");
        output.WriteLine("Commands:");
        output.WriteLine("  enqueue  Add a proposed commit entry to the queue");
        output.WriteLine("  status   Show the current commit queue");
        output.WriteLine("  changes  Show working tree files and queue assignment");
        output.WriteLine("  inspect  Show one queued entry");
        output.WriteLine("  edit     Begin, save, abort, or inspect an edit session");
        output.WriteLine("  guard    Fail while queued or unsafe changes remain");
        output.WriteLine("  next     Stage the first queued entry and advance the queue");
        output.WriteLine("  clear    Remove all entries from the queue");
        return 0;
    }

    private void WriteFiles(string label, IReadOnlyList<string> files)
    {
        output.WriteLine($"{label}: {(files.Count == 0 ? "(none)" : "")}");
        foreach (var file in files)
            output.WriteLine($"  {file}");
    }
}

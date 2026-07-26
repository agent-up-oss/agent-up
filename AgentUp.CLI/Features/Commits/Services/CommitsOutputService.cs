using AgentUp.CLI.Features.Commits.DTOs;
using AgentUp.CLI.Features.Commits.Models;

namespace AgentUp.CLI.Features.Commits.Services;

public sealed class CommitsOutputService(TextWriter output)
{
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

        if (result.UnassignedFiles.Count > 0)
        {
            output.WriteLine();
            output.WriteLine($"Warning: {result.UnassignedFiles.Count} modified file(s) not in any queued entry:");
            foreach (var file in result.UnassignedFiles)
                output.WriteLine($"  {file}");
        }

        return 0;
    }

    public int WriteStagingResult(CommitsStagingResult result)
    {
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

    public int WriteHelp()
    {
        output.WriteLine("Usage: agentup commits <command>");
        output.WriteLine("Commands:");
        output.WriteLine("  enqueue  Add a proposed commit entry to the queue");
        output.WriteLine("  status   Show the current commit queue");
        output.WriteLine("  next     Stage the first queued entry and advance the queue");
        output.WriteLine("  clear    Remove all entries from the queue");
        return 0;
    }
}

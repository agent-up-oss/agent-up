using AgentUp.TestAgents.Features.Inference.Interfaces;
using AgentUp.TestAgents.Features.Inference.Models;

namespace AgentUp.TestAgents.Features.Inference.Providers;

/// <summary>
/// Runs the four loop-coverage tools inside a worktree. Paths are resolved under that root;
/// a path that escapes it is a validation failure rather than a write elsewhere.
/// </summary>
public sealed class WorktreeToolExecutor : IWorktreeTools
{
    public IReadOnlySet<string> Names { get; } = new HashSet<string>(StringComparer.Ordinal)
    {
        "complete_task", "read_file", "write_file", "list_files"
    };

    public ToolExecutionResult Execute(string worktree, ParsedToolCall call) =>
        call.Name switch
        {
            "complete_task" => Complete(call),
            "read_file" => Read(worktree, call),
            "write_file" => Write(worktree, call),
            "list_files" => List(worktree, call),
            _ => new ToolExecutionResult(false, $"Unknown tool '{call.Name}'.", false)
        };

    private static ToolExecutionResult Complete(ParsedToolCall call)
    {
        var summary = call.Arguments.TryGetValue("summary", out var value) ? value : string.Empty;
        if (string.IsNullOrWhiteSpace(summary))
            return new ToolExecutionResult(false, "complete_task requires summary.", false);
        return new ToolExecutionResult(true, summary, true);
    }

    private static ToolExecutionResult Read(string worktree, ParsedToolCall call)
    {
        if (!TryResolve(worktree, call.Arguments.GetValueOrDefault("path"), out var path, out var error))
            return new ToolExecutionResult(false, error, false);
        try
        {
            return new ToolExecutionResult(true, File.ReadAllText(path), false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ToolExecutionResult(false, exception.Message, false);
        }
    }

    private static ToolExecutionResult Write(string worktree, ParsedToolCall call)
    {
        if (!TryResolve(worktree, call.Arguments.GetValueOrDefault("path"), out var path, out var error))
            return new ToolExecutionResult(false, error, false);
        var content = call.Arguments.GetValueOrDefault("content") ?? string.Empty;
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, content);
            return new ToolExecutionResult(true, $"Wrote {call.Arguments.GetValueOrDefault("path")}.", false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ToolExecutionResult(false, exception.Message, false);
        }
    }

    private static ToolExecutionResult List(string worktree, ParsedToolCall call)
    {
        var relative = call.Arguments.GetValueOrDefault("path") ?? ".";
        if (!TryResolve(worktree, relative, out var path, out var error))
            return new ToolExecutionResult(false, error, false);
        try
        {
            if (!Directory.Exists(path))
                return new ToolExecutionResult(false, $"Directory '{relative}' does not exist.", false);
            var names = Directory.GetFileSystemEntries(path)
                .Select(entry => Path.GetFileName(entry))
                .Order(StringComparer.Ordinal);
            return new ToolExecutionResult(true, string.Join('\n', names), false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ToolExecutionResult(false, exception.Message, false);
        }
    }

    internal static bool TryResolve(string worktree, string? relative, out string path, out string error)
    {
        path = string.Empty;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(relative))
        {
            error = "A path is required.";
            return false;
        }

        var root = Path.GetFullPath(worktree);
        var combined = Path.IsPathRooted(relative)
            ? Path.GetFullPath(relative)
            : Path.GetFullPath(Path.Join(root, relative));
        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!combined.Equals(root, StringComparison.Ordinal) &&
            !combined.StartsWith(prefix, StringComparison.Ordinal))
        {
            error = $"Path '{relative}' is outside the worktree.";
            return false;
        }

        path = combined;
        return true;
    }
}

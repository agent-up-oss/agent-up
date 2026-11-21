using AgentUp.TestAgents.Features.Scripted.Interfaces;

namespace AgentUp.TestAgents.Features.Scripted.Providers;

/// <summary>
/// Writes a play's file the way a coding agent edits its checkout. A path that would leave the
/// working directory is refused rather than written, and so is a write the sandbox forbids.
/// </summary>
public sealed class WorkingDirectoryFileWriter : IScriptedFileWriter
{
    public async Task<string?> WriteAsync(string workingDirectory, string path, string content, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(workingDirectory);
        var target = Path.GetFullPath(Path.Join(root, path));
        if (!target.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return $"{path} is outside the working directory.";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllTextAsync(target, content, cancellationToken);
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception.Message;
        }
    }
}

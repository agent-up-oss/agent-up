namespace AgentUp.TestAgents.Features.Scripted.Interfaces;

/// <summary>Writes a file a play changes, under the session's working directory and nowhere else.</summary>
public interface IScriptedFileWriter
{
    /// <summary>Writes the file and returns null, or says why it was refused.</summary>
    Task<string?> WriteAsync(string workingDirectory, string path, string content, CancellationToken cancellationToken);
}

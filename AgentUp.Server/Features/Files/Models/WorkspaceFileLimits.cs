namespace AgentUp.Server.Features.Files.Models;

/// <summary>How much of a workspace one request may carry to a client.</summary>
public static class WorkspaceFileLimits
{
    /// <summary>A folder with more entries than this is cut, and the listing says so.</summary>
    public const int MaxEntries = 2000;

    /// <summary>A file larger than this is described but not sent, and so cannot be edited.</summary>
    public const long MaxContentBytes = 1024 * 1024;

    /// <summary>How far into a file the binary check looks for a NUL byte.</summary>
    public const int BinaryProbeBytes = 8000;
}

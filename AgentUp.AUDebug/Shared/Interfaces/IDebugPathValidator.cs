namespace AgentUp.AUDebug.Shared.Interfaces;

public interface IDebugPathValidator
{
    string RepositoryRoot { get; }
    string JoinUnderRoot(params string[] segments);
    string EnsureUnderRoot(string path);
    string SessionDirectory { get; }
    string LogsDirectory { get; }
    string ScreenshotsDirectory { get; }
}

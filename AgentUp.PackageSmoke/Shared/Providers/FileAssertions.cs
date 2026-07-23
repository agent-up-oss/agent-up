using AgentUp.PackageSmoke.Features.PackageValidation.DTOs;
using AgentUp.PackageSmoke.Shared.Interfaces;

namespace AgentUp.PackageSmoke.Shared.Providers;

public sealed class FileAssertions : IFindingSink
{
    private readonly List<SmokeFinding> _findings = [];

    public IReadOnlyList<SmokeFinding> Findings => _findings;

    public void FileExists(string path, string code)
    {
        var safePath = SafeObservedPath(path);
        if (safePath is null || !File.Exists(safePath))
            Error(code, $"Expected file missing: {path}");
    }

    public void ExecutableExists(string path, string code)
    {
        FileExists(path, code);
        var safePath = SafeObservedPath(path);
        if (!OperatingSystem.IsWindows() && safePath is not null && File.Exists(safePath))
        {
            try
            {
                var mode = File.GetUnixFileMode(safePath);
                if (!mode.HasFlag(UnixFileMode.UserExecute))
                    Error(code, $"Expected executable missing execute bit: {path}");
            }
            catch (FileNotFoundException)
            {
                Error(code, $"Expected executable is a dangling symlink: {path}");
            }
        }
    }

    public void SymlinkExists(string path, string code)
    {
        var safePath = SafeObservedPath(path);
        if (safePath is null)
        {
            Error(code, $"Expected symlink missing: {path}");
            return;
        }

        var info = new FileInfo(safePath);
        if (!File.Exists(safePath) && !Directory.Exists(safePath) && string.IsNullOrEmpty(info.LinkTarget))
        {
            Error(code, $"Expected symlink missing: {path}");
            return;
        }

        if (string.IsNullOrEmpty(info.LinkTarget))
            Error(code, $"Expected symlink but found regular path: {path}");
    }

    public void Contains(string path, string expected, string code)
    {
        FileExists(path, code);
        var safePath = SafeObservedPath(path);
        if (safePath is not null && File.Exists(safePath) && !File.ReadAllText(safePath).Contains(expected, StringComparison.Ordinal))
            Error(code, $"Expected {path} to contain: {expected}");
    }

    public void Info(string code, string message)
        => _findings.Add(new SmokeFinding(FindingSeverity.Info, code, message));

    public void Error(string code, string message)
        => _findings.Add(new SmokeFinding(FindingSeverity.Error, code, message));

    private static string? SafeObservedPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return Path.GetFullPath(path);
    }
}

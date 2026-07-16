using AgentUp.PackageSmoke.Features.Validation.DTOs;

namespace AgentUp.PackageSmoke.Features.Validation.Services;

public sealed class FileAssertions
{
    private readonly List<SmokeFinding> _findings = [];

    public IReadOnlyList<SmokeFinding> Findings => _findings;

    public void FileExists(string path, string code)
    {
        if (!File.Exists(path))
            Error(code, $"Expected file missing: {path}");
    }

    public void ExecutableExists(string path, string code)
    {
        FileExists(path, code);
        if (!OperatingSystem.IsWindows() && File.Exists(path))
        {
            try
            {
                var mode = File.GetUnixFileMode(path);
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
        var info = new FileInfo(path);
        if (!File.Exists(path) && !Directory.Exists(path) && string.IsNullOrEmpty(info.LinkTarget))
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
        if (File.Exists(path) && !File.ReadAllText(path).Contains(expected, StringComparison.Ordinal))
            Error(code, $"Expected {path} to contain: {expected}");
    }

    public void Info(string code, string message)
        => _findings.Add(new SmokeFinding(FindingSeverity.Info, code, message));

    public void Error(string code, string message)
        => _findings.Add(new SmokeFinding(FindingSeverity.Error, code, message));
}

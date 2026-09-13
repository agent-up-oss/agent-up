using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using AgentUp.Server.Features.DesktopApplications.Interfaces;

namespace AgentUp.Server.Features.DesktopApplications.Providers;

public sealed class HostedDesktopNativeLibraryProvider : IHostedDesktopNativeLibraryProvider
{
    private static readonly object Gate = new();
    private static readonly Dictionary<string, string?> NixLibraryPathByShell = new(StringComparer.Ordinal);
    private readonly Func<string, string?> _environment;

    public HostedDesktopNativeLibraryProvider()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    internal HostedDesktopNativeLibraryProvider(Func<string, string?> environment)
    {
        _environment = environment;
    }

    public IReadOnlyDictionary<string, string> CreateEnvironment(string? searchRoot)
    {
        var path = MergeLibraryPath(
            ImportNixShellLibraryPath(searchRoot),
            _environment("NIX_LD_LIBRARY_PATH"),
            _environment("LD_LIBRARY_PATH"));
        return string.IsNullOrWhiteSpace(path)
            ? new Dictionary<string, string>()
            : new Dictionary<string, string> { ["LD_LIBRARY_PATH"] = path };
    }

    internal static string MergeLibraryPath(params string?[] parts) =>
        string.Join(':', parts
            .Where(static part => !string.IsNullOrWhiteSpace(part))
            .SelectMany(static part => part!.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal));

    private string? ImportNixShellLibraryPath(string? searchRoot)
    {
        if (!OperatingSystem.IsLinux())
            return null;

        var shellNix = FindShellNix(searchRoot, AppContext.BaseDirectory, Environment.CurrentDirectory);
        if (shellNix is null)
            return null;

        lock (Gate)
        {
            if (NixLibraryPathByShell.TryGetValue(shellNix, out var cached))
                return cached;

            var imported = RunNixShellLibraryPath(shellNix);
            NixLibraryPathByShell[shellNix] = imported;
            return imported;
        }
    }

    internal static string? FindShellNix(params string?[] roots) =>
        roots.Select(WalkForShellNix).FirstOrDefault(static found => found is not null);

    private static string? WalkForShellNix(string? start)
    {
        var directory = TryGetFullPath(start);
        if (directory is null)
            return null;

        if (File.Exists(directory))
            directory = Path.GetDirectoryName(directory);

        while (!string.IsNullOrEmpty(directory))
        {
            var candidate = Path.Join(directory, "shell.nix");
            if (File.Exists(candidate))
                return candidate;

            var parent = Path.GetDirectoryName(directory);
            if (parent == directory)
                break;
            directory = parent;
        }

        return null;
    }

    private static string? TryGetFullPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or IOException)
        {
            return null;
        }
    }

    private static string? RunNixShellLibraryPath(string shellNix)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "nix-shell",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.StartInfo.ArgumentList.Add(shellNix);
            process.StartInfo.ArgumentList.Add("--run");
            process.StartInfo.ArgumentList.Add("printf '__AGENTUP_NIX_LD__%s\\n' \"$LD_LIBRARY_PATH\"");
            var stdout = new StringBuilder();
            process.OutputDataReceived += (_, args) =>
            {
                if (args.Data is not null)
                    stdout.AppendLine(args.Data);
            };
            process.ErrorDataReceived += (_, _) => { };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            if (!process.WaitForExit(90_000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (InvalidOperationException ex)
                {
                    Trace.TraceWarning(ex.Message);
                }

                return null;
            }

            var line = stdout.ToString()
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .LastOrDefault(value => value.StartsWith("__AGENTUP_NIX_LD__", StringComparison.Ordinal));
            var imported = line is null ? null : line["__AGENTUP_NIX_LD__".Length..];
            return string.IsNullOrWhiteSpace(imported) ? null : imported;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}

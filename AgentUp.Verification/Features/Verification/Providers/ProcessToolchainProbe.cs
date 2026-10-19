using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using AgentUp.Verification.Features.Verification.Interfaces;
using AgentUp.Verification.Features.Verification.Models;

namespace AgentUp.Verification.Features.Verification.Providers;

/// <summary>
/// Finds a toolchain on PATH and reads its version. The id must already be in
/// <see cref="ToolchainCatalog"/>; this probe does not accept arbitrary executable names.
/// </summary>
public sealed class ProcessToolchainProbe : IToolchainProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(5);

    public ToolchainObservation Probe(string toolchainId)
        => Probe(toolchainId, Environment.GetEnvironmentVariable("PATH"));

    public ToolchainObservation Probe(string toolchainId, string? path)
    {
        if (!ToolchainCatalog.IsKnown(toolchainId))
            return ToolchainObservation.Missing;

        var executable = Find(toolchainId, path);
        if (executable is null)
            return ToolchainObservation.Missing;

        try
        {
            return ReadVersion(executable);
        }
        catch (Win32Exception)
        {
            return ToolchainObservation.Missing;
        }
        catch (IOException)
        {
            return ToolchainObservation.Missing;
        }
    }

    private static ToolchainObservation ReadVersion(string executable)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = executable,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        process.StartInfo.ArgumentList.Add("--version");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, eventArgs) => Append(stdout, eventArgs.Data);
        process.ErrorDataReceived += (_, eventArgs) => Append(stderr, eventArgs.Data);

        if (!process.Start())
            return ToolchainObservation.Missing;

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (!process.WaitForExit(ProbeTimeout))
        {
            process.Kill(entireProcessTree: true);
            return ToolchainObservation.Missing;
        }

        process.WaitForExit();
        if (stderr.Length > 0)
        {
            if (stdout.Length > 0)
                stdout.Append('\n');
            stdout.Append(stderr);
        }

        var version = ToolchainVersion.Read(stdout.ToString());
        if (process.ExitCode != 0 && version is null)
            return ToolchainObservation.Missing;

        return new ToolchainObservation(true, version);
    }

    private static void Append(StringBuilder builder, string? line)
    {
        if (line is null)
            return;

        if (builder.Length > 0)
            builder.Append('\n');

        builder.Append(line);
    }

    private static string? Find(string toolchainId, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var candidate in Candidates(toolchainId))
            {
                var fullPath = Path.Join(directory, candidate);
                if (File.Exists(fullPath))
                    return fullPath;
            }
        }

        return null;
    }

    private static IEnumerable<string> Candidates(string toolchainId)
    {
        yield return toolchainId;
        if (!OperatingSystem.IsWindows())
            yield break;

        yield return toolchainId + ".exe";
        yield return toolchainId + ".cmd";
    }
}

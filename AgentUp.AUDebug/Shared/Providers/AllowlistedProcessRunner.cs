using System.Diagnostics;
using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Shared.Providers;

public sealed class AllowlistedProcessRunner : IAllowlistedProcessRunner
{
    private static readonly HashSet<string> DefaultAllowlist = new(StringComparer.Ordinal)
    {
        "bash",
        "chromium",
        "chromium-browser",
        "dotnet",
        "google-chrome",
        "import",
        "kill",
        "nix-shell",
        "node",
        "npm",
        "setsid",
        "true",
        "xdotool"
    };

    private readonly IReadOnlySet<string> _allowlist;

    public AllowlistedProcessRunner()
        : this(DefaultAllowlist)
    {
    }

    public AllowlistedProcessRunner(IReadOnlySet<string> allowlist)
        => _allowlist = allowlist;

    public Process Start(AllowlistedCommand command)
    {
        var process = new Process { StartInfo = CreateStartInfo(command) };
        if (!process.Start())
            throw new InvalidOperationException($"Could not start '{command.FileName}'.");
        WriteInput(process, command.StandardInput);
        return process;
    }

    public async Task<ProcessResult> RunAsync(AllowlistedCommand command, CancellationToken cancellationToken)
    {
        using var process = new Process { StartInfo = CreateStartInfo(command) };
        if (!process.Start())
            throw new InvalidOperationException($"Could not start '{command.FileName}'.");

        WriteInput(process, command.StandardInput);
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            KillTree(process.Id);
            throw;
        }

        return new ProcessResult(process.ExitCode, await stdout, await stderr);
    }

    public void KillTree(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            System.Diagnostics.Trace.WriteLine(exception.Message);
        }
    }

    public bool IsRunning(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return exception is not ArgumentException && exception is not InvalidOperationException;
        }
    }

    private ProcessStartInfo CreateStartInfo(AllowlistedCommand command)
    {
        var executable = Path.GetFileName(command.FileName);
        if (!_allowlist.Contains(executable) && !_allowlist.Contains(command.FileName))
            throw new InvalidOperationException($"Command '{command.FileName}' is not allowlisted for au-debug.");

        var startInfo = new ProcessStartInfo
        {
            FileName = command.FileName,
            WorkingDirectory = command.WorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = command.StandardInput is not null,
            UseShellExecute = false
        };

        foreach (var argument in command.Arguments)
            startInfo.ArgumentList.Add(argument);

        if (command.Environment is null)
            return startInfo;

        foreach (var pair in command.Environment)
            startInfo.Environment[pair.Key] = pair.Value;

        return startInfo;
    }

    private static void WriteInput(Process process, string? input)
    {
        if (input is null)
            return;

        process.StandardInput.Write(input);
        process.StandardInput.Close();
    }
}

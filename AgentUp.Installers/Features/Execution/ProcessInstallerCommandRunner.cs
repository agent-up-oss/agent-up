using AgentUp.Installers.Features.Prerequisites;

namespace AgentUp.Installers.Features.Execution;

public sealed class ProcessInstallerCommandRunner : ICommandRunner
{
    public async Task<ProcessResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        System.Diagnostics.Process? process;
        try
        {
            process = System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            return new ProcessResult(127, "", ex.Message);
        }

        if (process is null)
            return new ProcessResult(127, "", $"Failed to start {fileName}.");

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderr = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            return new ProcessResult(process.ExitCode, await stdout, await stderr);
        }
    }
}

using AgentUp.AUDebug.Shared.Interfaces;

namespace AgentUp.AUDebug.Shared.Providers;

/// <summary>
/// Runs an X11 command-line tool against the debug display, from PATH when the host has it
/// and through <c>nix-shell -p</c> when it does not.
/// </summary>
/// <remarks>
/// Shared because both the Desktop slice and the Screens slice drive the same window with
/// the same two tools, and a NixOS workstation resolves them differently from a CI runner.
/// </remarks>
public static class X11ToolRunner
{
    public static Task<ProcessResult> RunAsync(
        IAllowlistedProcessRunner processes,
        IDebugEnvironment environment,
        string repositoryRoot,
        string executable,
        string nixPackage,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        string? standardInput = null)
    {
        var display = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DISPLAY"] = environment.Display
        };
        if (environment.FindOnPath(executable) is not null)
        {
            return processes.RunAsync(
                new AllowlistedCommand(executable, arguments, repositoryRoot, display, standardInput),
                cancellationToken);
        }

        var quoted = string.Join(' ', arguments.Select(BashQuote.Single));
        return processes.RunAsync(
            new AllowlistedCommand(
                "nix-shell",
                ["-p", nixPackage, "--run", $"{executable} {quoted}"],
                repositoryRoot,
                display,
                standardInput),
            cancellationToken);
    }
}

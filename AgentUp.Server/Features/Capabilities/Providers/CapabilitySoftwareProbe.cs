using AgentUp.Sdk.Common;
using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Features.Capabilities.Providers;

/// <summary>
/// Looks for a capability's software on the Server host: Nix for what the capability delivers
/// itself, and the Server's search path plus the development tool directories for the rest.
/// </summary>
public sealed class CapabilitySoftwareProbe(
    INixPresenceProvider nix,
    CapabilityRuntimePathProvider runtime,
    Func<string?>? path = null) : ICapabilitySoftwareProbe
{
    public const string Nix = "nix";

    public IReadOnlyList<string> Missing(IReadOnlyList<CapabilitySoftware> software)
    {
        var missing = new List<string>();
        foreach (var item in software)
        {
            var name = item.DeliveredByNix ? Nix : item.Executable;
            var found = item.DeliveredByNix ? nix.IsAvailable : IsOnSearchPath(item.Executable);
            if (!found && !missing.Contains(name, StringComparer.Ordinal))
                missing.Add(name);
        }

        return missing;
    }

    private bool IsOnSearchPath(string executable)
    {
        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD").Split(';', StringSplitOptions.RemoveEmptyEntries)
            : [string.Empty];
        var directories = ((path ?? (() => Environment.GetEnvironmentVariable("PATH")))() ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Concat(runtime.Directories())
            .Distinct(StringComparer.Ordinal);
        return directories
            .SelectMany(directory => extensions.Select(extension => Path.Join(directory, executable + extension)))
            .Any(IsExecutable);
    }

    private static bool IsExecutable(string candidate)
    {
        try
        {
            if (!File.Exists(candidate))
                return false;
            if (OperatingSystem.IsWindows())
                return true;
            var mode = File.GetUnixFileMode(candidate);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

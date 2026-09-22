using AgentUp.Server.Features.Capabilities.Interfaces;

namespace AgentUp.Server.Features.Capabilities.Providers;

public sealed class NixPresenceProvider : INixPresenceProvider
{
    public NixPresenceProvider() : this(DetectInstalledNix)
    {
    }

    public NixPresenceProvider(Func<bool> detect)
    {
        IsAvailable = !OperatingSystem.IsWindows() && detect();
    }

    public bool IsAvailable { get; }

    private static bool DetectInstalledNix()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                   .Any(directory => File.Exists(Path.Join(directory, "nix")))
               || File.Exists("/nix/var/nix/profiles/default/bin/nix");
    }
}

using LocalInstaller.Core.Features.NixOsInstallation.Interfaces;

namespace LocalInstaller.Core.Features.NixOsInstallation.Providers;

public sealed class NixOsPathExecutableLookup : INixOsExecutableLookup
{
    public string? Find(string executableName)
    {
        var paths = (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var directory in paths)
        {
            var candidate = Path.Join(directory, executableName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}

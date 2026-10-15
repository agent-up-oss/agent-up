using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;

namespace AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

public sealed class CapabilityExecutableProbe : ICapabilityExecutableProbe
{
    private readonly bool _treatExistingFilesAsExecutable;
    private readonly Func<string, UnixFileMode> _unixFileMode;

    public CapabilityExecutableProbe()
        : this(OperatingSystem.IsWindows())
    {
    }

    public CapabilityExecutableProbe(bool treatExistingFilesAsExecutable, Func<string, UnixFileMode>? unixFileMode = null)
    {
        _treatExistingFilesAsExecutable = treatExistingFilesAsExecutable;
        _unixFileMode = unixFileMode ?? File.GetUnixFileMode;
    }

    public bool IsExecutable(string path)
    {
        try
        {
            if (!File.Exists(path))
                return false;
            if (_treatExistingFilesAsExecutable)
                return true;
            var mode = _unixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

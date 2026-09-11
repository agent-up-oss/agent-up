using AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Interfaces;

namespace AgentUp.Capabilities.Common.Features.CapabilityDiscovery.Providers;

public sealed class CapabilityExecutableProbe : ICapabilityExecutableProbe
{
    public bool IsExecutable(string path)
    {
        try
        {
            if (!File.Exists(path))
                return false;
            if (OperatingSystem.IsWindows())
                return true;
            var mode = File.GetUnixFileMode(path);
            return (mode & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}

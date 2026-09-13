namespace AgentUp.Server.Features.DesktopApplications.Interfaces;

public interface IHostedDesktopNativeLibraryProvider
{
    IReadOnlyDictionary<string, string> CreateEnvironment(string? searchRoot);
}

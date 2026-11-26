using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// Restricts the agent loopback socket to the Server user once Kestrel has created it, and
/// removes the file on shutdown.
/// </summary>
public sealed class AgentLoopbackSocketPermissionHost(
    AgentLoopbackEndpoint endpoint,
    IHostApplicationLifetime lifetime,
    ILogger<AgentLoopbackSocketPermissionHost> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        lifetime.ApplicationStarted.Register(Restrict);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            File.Delete(endpoint.SocketPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(exception, "Could not remove the agent loopback socket.");
        }

        return Task.CompletedTask;
    }

    private void Restrict()
    {
        if (!File.Exists(endpoint.SocketPath) || OperatingSystem.IsWindows())
            return;
        try
        {
            File.SetUnixFileMode(endpoint.SocketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogDebug(exception, "Could not restrict the agent loopback socket.");
        }
    }
}

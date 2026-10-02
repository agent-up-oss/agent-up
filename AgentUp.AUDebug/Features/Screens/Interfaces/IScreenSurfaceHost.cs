namespace AgentUp.AUDebug.Features.Screens.Interfaces;

/// <summary>
/// Starts whatever a surface needs to be driven, and stops only what it started.
/// </summary>
/// <remarks>
/// Product screens run against Demo, so neither surface needs a Server. A surface that is
/// already up - because a maintainer is running <c>au-debug up</c> - is reused untouched.
/// </remarks>
public interface IScreenSurfaceHost : IAsyncDisposable
{
    Task StartAsync(string surface, CancellationToken cancellationToken);
}

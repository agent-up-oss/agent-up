using AgentUp.Server.Features.Authentication.Interfaces;

namespace AgentUp.Server.Tests.Fake;

internal sealed class FakeMcpRemoteAccess(bool isEnabled) : IMcpRemoteAccess
{
    public bool IsEnabled { get; } = isEnabled;
}

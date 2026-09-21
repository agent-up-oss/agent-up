using AgentUp.Server.Features.Connection.DTOs;
using AgentUp.Server.Features.Connection.Providers;

namespace AgentUp.Server.Features.Connection.Services;

public sealed class ConnectionService(ConnectionMetadataProvider metadata)
{
    public ConnectionMetadataDto Current() => metadata.Current();
}

using AgentUp.Desktop.Features.Authentication.Interfaces;
using AgentUp.Desktop.Features.Authentication.Models;

namespace AgentUp.Desktop.Features.Authentication.Providers;

public sealed class InMemoryServerConnectionStore : IServerConnectionStore
{
    private ServerSelection _selection = new();

    public ServerSelection Load() => Clone(_selection);

    public void Save(ServerSelection selection) => _selection = Clone(selection);

    private static ServerSelection Clone(ServerSelection selection)
        => new()
        {
            ActiveServerId = selection.ActiveServerId,
            Servers = selection.Servers
                .Select(server => new ConfiguredServer
                {
                    Id = server.Id,
                    Url = server.Url,
                    AccessToken = server.AccessToken
                })
                .ToList()
        };
}

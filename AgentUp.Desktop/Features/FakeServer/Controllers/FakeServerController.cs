using AgentUp.Desktop.Features.FakeServer.DTOs;
using AgentUp.Desktop.Features.FakeServer.Models;
using AgentUp.Desktop.Features.FakeServer.Services;

namespace AgentUp.Desktop.Features.FakeServer.Controllers;

public sealed class FakeServerController(FakeBackendService backend)
{
    public FakeServerConnectionDto Catalog(string? activeServerId)
        => backend.Catalog(activeServerId);

    public bool Matches(Uri? uri)
        => backend.Matches(uri);

    public bool Matches(string? url)
        => backend.Matches(url);

    public string? ApplicationHtml(int allocatedPort)
        => backend.ApplicationHtml(allocatedPort);

    public void Reset()
        => backend.Reset();
}

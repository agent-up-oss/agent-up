namespace AgentUp.Desktop.Features.Authentication.Models;

public sealed class ServerSelection
{
    public List<ConfiguredServer> Servers { get; set; } = [];
    public string? ActiveServerId { get; set; }
}

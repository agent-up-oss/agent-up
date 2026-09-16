namespace AgentUp.Desktop.Features.Authentication.Models;

public sealed class ConfiguredServer
{
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
    public string? AccessToken { get; set; }
}

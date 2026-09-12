using System.Text.Json;

namespace AgentUp.Server.Features.DesktopApplications.Providers;

public sealed class DesktopInputMessageProvider
{
    public DesktopInputMessage? Parse(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<DesktopInputMessage>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public sealed record DesktopInputMessage(
    string Type,
    int X = 0,
    int Y = 0,
    int Button = 0,
    double DeltaX = 0,
    double DeltaY = 0,
    string? Key = null);

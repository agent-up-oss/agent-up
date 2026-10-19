using System.Text.Json;

namespace AgentUp.Verification.Features.Verification.Models;

/// <summary>JSON for a program. Camel case, indented, no extra envelope.</summary>
public static class RunReportJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    public static string Serialize(RunReport report)
        => JsonSerializer.Serialize(report, Options);
}

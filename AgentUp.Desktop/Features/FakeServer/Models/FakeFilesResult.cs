using System.Text.Json.Nodes;

namespace AgentUp.Desktop.Features.FakeServer.Models;

/// <summary>What the Demo answers a Files request with: a body, or a problem with its status.</summary>
public sealed record FakeFilesResult(int Status, JsonObject Body, FakeGitFile? Change = null)
{
    public static FakeFilesResult Ok(JsonObject body, FakeGitFile? change = null) => new(200, body, change);

    public static FakeFilesResult Problem(int status, string title, string detail)
        => new(status, new JsonObject { ["title"] = title, ["detail"] = detail, ["status"] = status });
}

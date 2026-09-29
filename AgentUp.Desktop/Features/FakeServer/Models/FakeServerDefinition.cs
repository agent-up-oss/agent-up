using System.Text.Json.Nodes;

namespace AgentUp.Desktop.Features.FakeServer.Models;

public sealed class FakeServerDefinition
{
    public FakeServerDefinition(JsonNode root)
    {
        Root = root;
    }

    public JsonNode Root { get; }

    public string Id => RequiredString("id");

    public string Url => RequiredString("url");

    public string DisplayName => RequiredString("displayName");

    public JsonNode Connection => RequiredNode("connection");

    public JsonNode Authentication => RequiredNode("authentication");

    public JsonNode Entitlements => RequiredNode("entitlements");

    public JsonArray Workspaces => RequiredNode("workspaces").AsArray();

    public JsonNode? Overview => Root["overview"];

    public JsonNode? Git => Root["git"];

    public JsonNode? Console => Root["console"];

    public JsonNode? Agents => Root["agents"];

    public JsonNode? Pages => Root["pages"];

    public JsonNode? Capabilities => Root["capabilities"];

    public FakeServerDefinition Clone()
        => new(Root.DeepClone());

    private string RequiredString(string name)
        => Root[name]?.GetValue<string>()
           ?? throw new InvalidOperationException($"The fake server definition is missing '{name}'.");

    private JsonNode RequiredNode(string name)
        => Root[name]
           ?? throw new InvalidOperationException($"The fake server definition is missing '{name}'.");
}

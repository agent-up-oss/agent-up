using System.Text.Json;
using System.Text.Json.Nodes;
using AgentUp.Desktop.Features.FakeServer.Models;

namespace AgentUp.Desktop.Features.FakeServer.Providers;

public sealed class FakeServerDefinitionProvider
{
    public const string ResourceName = "AgentUp.FakeServer.definition.json";

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public FakeServerDefinition LoadEmbedded()
    {
        var assembly = typeof(FakeServerDefinitionProvider).Assembly;
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"The fake server definition resource '{ResourceName}' is missing.");
        return Load(stream);
    }

    public FakeServerDefinition Load(Stream stream)
    {
        var root = JsonNode.Parse(stream, documentOptions: new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }, nodeOptions: new JsonNodeOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("The fake server definition is empty.");
        var definition = new FakeServerDefinition(root);
        Validate(definition);
        return definition;
    }

    public FakeServerDefinition LoadJson(string json)
    {
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return Load(stream);
    }

    private static void Validate(FakeServerDefinition definition)
    {
        if (!string.Equals(definition.Id, FakeServerIdentity.Id, StringComparison.Ordinal))
            throw new InvalidOperationException("The fake server definition id does not match the client catalog.");
        if (!FakeServerIdentity.Matches(definition.Url))
            throw new InvalidOperationException("The fake server definition URL does not match the client catalog.");
        _ = Options;
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;
using AgentUp.Server.Features.Training.Interfaces;
using AgentUp.Server.Features.Training.Models;

namespace AgentUp.Server.Features.Training.Providers;

/// <summary>
/// The decision backlog on disk. Verdicts are written with it, so what a product owner judged
/// last week is still judged after a restart and is still there to export.
/// </summary>
public sealed class JsonProductDecisionStore : IProductDecisionStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _path;
    private readonly object _gate = new();

    public JsonProductDecisionStore(string dataDirectory)
        => _path = Path.Join(dataDirectory, "product-decisions.json");

    public IReadOnlyList<ProductDecision> Read()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
                return [];
            try
            {
                return JsonSerializer.Deserialize<List<ProductDecision>>(File.ReadAllText(_path), Json) ?? [];
            }
            catch (JsonException)
            {
                // A half-written file must not stop the Server starting. An empty backlog is
                // recoverable by recording again; a crash loop is not.
                return [];
            }
        }
    }

    public void Write(IReadOnlyList<ProductDecision> decisions)
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(_path, JsonSerializer.Serialize(decisions, Json));
        }
    }
}

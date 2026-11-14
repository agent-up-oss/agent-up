using System.Text.Json;
using AgentUp.Server.Features.TaskSource.Interfaces;
using AgentUp.Server.Features.TaskSource.Models;

namespace AgentUp.Server.Features.TaskSource.Providers;

/// <summary>The configured source on disk, so a restart keeps working the same backlog.</summary>
public sealed class JsonTaskSourceStore : ITaskSourceStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string _path;
    private readonly object _gate = new();

    public JsonTaskSourceStore(string dataDirectory)
        => _path = Path.Join(dataDirectory, "task-source.json");

    public TaskSourceSetting? Read()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
                return null;
            try
            {
                return JsonSerializer.Deserialize<TaskSourceSetting>(File.ReadAllText(_path), Json);
            }
            catch (JsonException)
            {
                // A half-written file must not stop the Server starting. Naming the source again
                // recovers it; a crash loop does not.
                return null;
            }
        }
    }

    public void Write(TaskSourceSetting? source)
    {
        lock (_gate)
        {
            if (source is null)
            {
                if (File.Exists(_path)) File.Delete(_path);
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(source, Json));
        }
    }
}

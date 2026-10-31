using System.Text.Json;
using AgentUp.Server.Features.Agents.Interfaces;
using AgentUp.Server.Features.Agents.Models;

namespace AgentUp.Server.Features.Agents.Providers;

/// <summary>
/// The picked queue on disk. Holds are written with it, so a workspace that was carrying a task
/// when the Server stopped still owns it when the Server comes back.
/// </summary>
public sealed class JsonTaskQueueStore : ITaskQueueStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _path;
    private readonly object _gate = new();

    public JsonTaskQueueStore(string dataDirectory) => _path = Path.Join(dataDirectory, "task-queue.json");

    public IReadOnlyList<QueuedTask> Read()
    {
        lock (_gate)
        {
            if (!File.Exists(_path))
                return [];
            try
            {
                return JsonSerializer.Deserialize<List<QueuedTask>>(File.ReadAllText(_path), Json) ?? [];
            }
            catch (JsonException)
            {
                // A half-written or hand-edited file must not stop the Server from starting; an
                // empty queue is recoverable by picking again, a crash loop is not.
                return [];
            }
        }
    }

    public void Write(IReadOnlyList<QueuedTask> tasks)
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(_path, JsonSerializer.Serialize(tasks, Json));
        }
    }
}
